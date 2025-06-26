using FabricTool.Common;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FabricTool.Fabric.Parameter
{
    public static class Utils
    {
        public static string ExtractFindValue(Dictionary<string, string> paramDict, string fileContent, bool filterMatch)
        {
            var findValue = paramDict.GetValueOrDefault("find_value");
            var isRegex = paramDict.GetValueOrDefault("is_regex", "").ToLower() == "true";

            if (isRegex && filterMatch)
            {
                var match = Regex.Match(fileContent, findValue);
                if (match.Success)
                {
                    if (match.Groups.Count != 2) // Group[0] is the entire match
                        throw new InputError($"Regex pattern '{findValue}' must contain exactly one capturing group.");

                    var matchedValue = match.Groups[1].Value;
                    if (string.IsNullOrEmpty(matchedValue))
                        throw new InputError($"Regex pattern '{findValue}' captured an empty value.");

                    return matchedValue;
                }
            }

            return findValue;
        }

        public static string ExtractReplaceValue(FabricWorkspace workspace, string replaceValue)
        {
            if (replaceValue == "$workspace.id")
                return workspace.WorkspaceId;

            if (replaceValue.StartsWith("$items"))
                return ExtractItemAttribute(workspace, replaceValue);

            return replaceValue;
        }

        private static string ExtractItemAttribute(FabricWorkspace workspace, string variable)
        {
            try
            {
                var parts = variable.Replace("$items.", "").Split('.');
                if (parts.Length != 3)
                    throw new InputError($"Invalid $items variable syntax: {variable}");

                var itemType = parts[0];
                var itemName = parts[1];
                var attribute = parts[2].ToLower();

                workspace.RefreshDeployedItemsAsync();

                if (!workspace.DeployedItems.TryGetValue(itemType, out var itemMap))
                    throw new InputError($"Item type '{itemType}' not found in deployed items");

                if (!itemMap.TryGetValue(itemName, out var item))
                    throw new InputError($"Item '{itemName}' not found as a deployed {itemType}");

                return attribute switch
                {
                    "id" => item.Guid,
                    //"sqlendpoint" => item.SqlEndpoint,
                    _ => throw new InputError($"Attribute '{attribute}' is invalid or not supported")
                };
            }
            catch (Exception ex)
            {
                throw new ParsingException($"Error parsing $items variable: {ex.Message}");
            }
        }

        public static string ReplaceKeyValue(Dictionary<string, object> paramDict, string jsonContent, string env)
        {
            var data = JsonNode.Parse(jsonContent) ?? throw new ParsingException("Invalid JSON content");
            var findKey = paramDict["find_key"].ToString();
            var replaceValue = ((JsonElement)paramDict["replace_value"]).Deserialize<Dictionary<string, string>>();

            if (replaceValue != null && replaceValue.TryGetValue(env, out var newValue))
            {
                var path = findKey.Trim('$').Split('.');
                var node = data;
                for (int i = 0; i < path.Length - 1; i++)
                    node = node?[path[i]];

                if (node != null && node[path.Last()] != null)
                    node[path.Last()] = newValue;
            }

            return data.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        public static string ReplaceVariablesInParameterFile(string rawFile)
        {
            foreach (var kvp in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>())
            {
                var key = kvp.Key.ToString();
                if (key?.StartsWith("$ENV:") == true)
                {
                    var placeholder = key;
                    var value = kvp.Value?.ToString() ?? "";
                    rawFile = rawFile.Replace(placeholder, value);
                }
            }
            return rawFile;
        }

        public static bool IsValidStructure(Dictionary<string, object> paramDict, string? paramName = null)
        {
            if (paramName != null)
                return paramDict.TryGetValue(paramName, out var val) && val is List<object>;

            var keys = new[] { "find_replace", "key_value_replace", "spark_pool" };
            var matching = keys.Where(k => paramDict.ContainsKey(k)).ToList();

            if (!matching.Any()) return false;
            return matching.All(k => paramDict[k] is List<object>);
        }
    }
}
