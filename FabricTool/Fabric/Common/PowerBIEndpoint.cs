using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Identity;

namespace FabricTool.Fabric.Common
{    
    public class PowerBiEndpint
    {
        private readonly HttpClient _httpClient;
        private readonly string _accessToken;

        public PowerBiEndpint()
        {
            _httpClient = new HttpClient();
            _accessToken = GetAccessToken().GetAwaiter().GetResult();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        private async Task<string> GetAccessToken()
        {
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(
                    new[] { "https://analysis.windows.net/powerbi/api/.default" }));
            return token.Token;
        }

        public async Task<string> PublishImportRdlFileAsync(string rdlFilePath, string groupId, string nameConflict = "Abort")
        {
            var fileName = Path.GetFileName(rdlFilePath);
            var boundary = "FormBoundary" + Guid.NewGuid().ToString("N");
            var fileBody = await File.ReadAllTextAsync(rdlFilePath);

            var content = new MultipartFormDataContent(boundary);
            var rdlContent = new StringContent(fileBody, Encoding.UTF8, "application/rdl");
            content.Add(rdlContent, fileName, fileName);

            var url = string.IsNullOrEmpty(groupId)
                ? $"https://api.powerbi.com/v1.0/myorg/imports?datasetDisplayName={fileName}&nameConflict={nameConflict}"
                : $"https://api.powerbi.com/v1.0/myorg/groups/{groupId}/imports?datasetDisplayName={fileName}&nameConflict={nameConflict}";

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("id").GetString();
        }

        public async Task SetBasicPasswordToRdlAsync(string id, string groupId, string userName, string password)
        {
            // Step 1: Get ReportId
            string reportId = null;
            while (reportId == null)
            {
                var importUrl = string.IsNullOrEmpty(groupId)
                    ? $"https://api.powerbi.com/v1.0/myorg/imports/{id}"
                    : $"https://api.powerbi.com/v1.0/myorg/groups/{groupId}/imports/{id}";

                var importResponse = await _httpClient.GetAsync(importUrl);
                importResponse.EnsureSuccessStatusCode();

                var importJson = await importResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(importJson);
                if (doc.RootElement.TryGetProperty("reports", out var reports)
                    && reports.TryGetProperty("id", out var reportIdProp))
                {
                    reportId = reportIdProp.GetString();
                }
            }

            // Step 2: Get Datasource Info
            var datasourcesUrl = string.IsNullOrEmpty(groupId)
                ? $"https://api.powerbi.com/v1.0/myorg/reports/{reportId}/datasources"
                : $"https://api.powerbi.com/v1.0/myorg/groups/{groupId}/reports/{reportId}/datasources";

            var datasourceResp = await _httpClient.GetAsync(datasourcesUrl);
            datasourceResp.EnsureSuccessStatusCode();

            var dsJson = await datasourceResp.Content.ReadAsStringAsync();
            using var dsDoc = JsonDocument.Parse(dsJson);
            var first = dsDoc.RootElement.GetProperty("value")[0];
            var gatewayId = first.GetProperty("gatewayId").GetString();
            var datasourceId = first.GetProperty("datasourceId").GetString();

            // Step 3: Patch Credentials
            var patchUrl = $"https://api.powerbi.com/v1.0/myorg/gateways/{gatewayId}/datasources/{datasourceId}";
            var body = new
            {
                credentialDetails = new
                {
                    credentialType = "Basic",
                    credentials = JsonSerializer.Serialize(new
                    {
                        credentialData = new[]
                        {
                        new { name = "username", value = userName },
                        new { name = "password", value = password }
                    }
                    }),
                    encryptedConnection = "Encrypted",
                    encryptionAlgorithm = "None",
                    privacyLevel = "None"
                }
            };

            var patchJson = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var patchResp = await _httpClient.PatchAsync(patchUrl, patchJson);
            patchResp.EnsureSuccessStatusCode();
        }
    }

}
