using Azure.Core;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FabricTool.Common;

public class FabricEndpoint
{
    private string? _aadToken;
    private DateTimeOffset? _aadTokenExpiration;
    private readonly TokenCredential _tokenCredential;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public FabricEndpoint(TokenCredential tokenCredential, HttpClient? httpClient = null)
    {
        _tokenCredential = tokenCredential;
        _httpClient = httpClient ?? new HttpClient();
        RefreshTokenAsync().GetAwaiter().GetResult();
    }

    public async Task<FabricResponse> InvokeAsync(HttpMethod method, string url, object? body = null, Dictionary<string, StreamContent>? files = null, int maxRetries = 5, CancellationToken cancellationToken = default)
    {
        int iterationCount = 0;
        bool exitLoop = false;
        bool longRunning = false;
        string bodyJson = body is string str ? str : JsonSerializer.Serialize(body, _jsonOptions);
        HttpResponseMessage? response = null;

        do
        {
            try
            {
                using var request = new HttpRequestMessage(method, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _aadToken);
                request.Headers.UserAgent.ParseAdd("FabricTool");

                if (files is not null)
                {
                    var multipart = new MultipartFormDataContent();
                    foreach (var kvp in files)
                        multipart.Add(kvp.Value, kvp.Key, kvp.Key);
                    request.Content = multipart;
                }
                else if (!string.IsNullOrWhiteSpace(bodyJson))
                {
                    request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                }

                response = await _httpClient.SendAsync(request);
                iterationCount++;

                Logging.LogDebug(await FormatInvokeLog(response, method.Method, url, bodyJson));

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                    response.Headers.TryGetValues("x-ms-public-api-error-code", out var codes) &&
                    codes.Contains("TokenExpired"))
                {
                    Logging.LogInformation("AAD token expired. Refreshing token.");
                    await RefreshTokenAsync(cancellationToken);
                    continue;
                }

                (exitLoop, method, url, bodyJson, longRunning) = await HandleResponse(response, method, url, bodyJson, longRunning, iterationCount, maxRetries);
            }
            catch (Exception ex)
            {
                var log = response is not null ? await FormatInvokeLog(response, method.Method, url, bodyJson) : "No response received.";
                Logging.LogError(log);
                throw;
            }
        } while (!exitLoop);

        var resultJson = await response.Content.ReadAsStringAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType;

        return new FabricResponse
        {
            StatusCode = (int)response.StatusCode,
            Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
            Body = contentType == "application/json" ? JsonSerializer.Deserialize<JsonObject>(resultJson, _jsonOptions) : null,
        };
    }

    private async Task RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_aadToken == null || _aadTokenExpiration == null || _aadTokenExpiration < DateTimeOffset.UtcNow)
        {
            try
            {
                string[] scopes = { "https://api.fabric.microsoft.com/.default" };
                var token = await _tokenCredential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken);
                _aadToken = token.Token;

                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(_aadToken);
                _aadTokenExpiration = DateTimeOffset.FromUnixTimeSeconds(long.Parse(jwt.Claims.First(c => c.Type == "exp").Value));

                if (jwt.Claims.Any(c => c.Type == "upn"))
                {
                    Logging.LogInformation("Executing as User '{User}'", jwt.Claims.First(c => c.Type == "upn").Value);
                }
                else if (jwt.Claims.Any(c => c.Type == "appid"))
                {
                    Logging.LogInformation("Executing as Application Id '{AppId}'", jwt.Claims.First(c => c.Type == "appid").Value);
                }
                else if (jwt.Claims.Any(c => c.Type == "oid"))
                {
                    Logging.LogInformation("Executing as Object Id '{Oid}'", jwt.Claims.First(c => c.Type == "oid").Value);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to acquire AAD token", ex);
            }
        }
    }

    private static async Task<string> FormatInvokeLog(HttpResponseMessage response, string method, string url, string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\nURL: {url}");
        sb.AppendLine($"Method: {method}");
        sb.AppendLine($"Request Body:\n{body}");
        sb.AppendLine($"Response Status: {(int)response.StatusCode}");
        sb.AppendLine("Response Headers:");
        foreach (var header in response.Headers)
            sb.AppendLine($"{header.Key}: {string.Join(",", header.Value)}");

        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var json = await response.Content.ReadAsStringAsync();
            sb.AppendLine("Response Body:");
            sb.AppendLine(json);
        }
        return sb.ToString();
    }

    private static async Task<(bool, HttpMethod, string, string, bool)> HandleResponse(
        HttpResponseMessage response,
        HttpMethod method,
        string url,
        string body,
        bool longRunning,
        int attempt,
        int maxRetries)
    {
        string? retryAfterHeader = response.Headers.TryGetValues("Retry-After", out var retryVals) ? retryVals.FirstOrDefault() : null;
        double retryAfter = double.TryParse(retryAfterHeader, out var parsed) ? parsed : 60;

        if ((response.StatusCode == System.Net.HttpStatusCode.OK && longRunning) || response.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            string location = response.Headers.Location?.ToString() ?? string.Empty;
            string responseJson = await response.Content.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<JsonObject>(responseJson);
            var status = payload?["status"]?.ToString();

            if (status == "Succeeded")
                return (true, method, url, body, false);
            else if (status == "Failed")
            {
                var errorMessage = payload?["error"]?["message"]?.ToString() ?? "Unknown error";
                throw new Exception($"Operation failed: {errorMessage}");
            }
            else if (status == "Undefined")
                throw new Exception("Operation is in an undefined state.");
            else
                await Task.Delay(TimeSpan.FromSeconds(1));

            return (false, HttpMethod.Get, location, "{}", true);
        }
        else if ((int)response.StatusCode == 429)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, attempt))));
            return (false, method, url, body, longRunning);
        }
        else if ((int)response.StatusCode == 400 || (int)response.StatusCode == 401 || (int)response.StatusCode == 403)
        {
            var msg = await response.Content.ReadAsStringAsync();
            throw new Exception($"Fabric API returned {(int)response.StatusCode}: {msg}");
        }
        else if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
        {
            return (true, method, url, body, longRunning);
        }
        else
        {
            var msg = await response.Content.ReadAsStringAsync();
            throw new Exception($"Unhandled error: {(int)response.StatusCode} {msg}");
        }
    }
}

public class FabricResponse
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public JsonObject? Body { get; set; }
}
