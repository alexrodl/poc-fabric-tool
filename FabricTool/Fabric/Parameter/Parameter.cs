using FabricTool.Common;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static FabricTool.Constants;

namespace FabricTool.Fabric.Parameter
{
    public class Parameter
    {
        private readonly string _repositoryDirectory;
        private readonly List<string> _itemTypeInScope;
        private readonly string _environment;
        private readonly string _parameterFileName;
        private readonly string _parameterFilePath;

        public Dictionary<string, dynamic> EnvironmentParameter { get; private set; } = new();
        public string LoadErrorMessage { get; private set; } = string.Empty;

        public static readonly Dictionary<string, (HashSet<string> Minimum, HashSet<string> Maximum)> ParameterKeys = new()
        {
            ["find_replace"] = (new HashSet<string> { "find_value", "replace_value" }, new HashSet<string> { "find_value", "replace_value", "is_regex", "item_type", "item_name", "file_path" }),
            ["spark_pool"] = (new HashSet<string> { "instance_pool_id", "replace_value" }, new HashSet<string> { "instance_pool_id", "replace_value", "item_name" }),
            ["key_value_replace"] = (new HashSet<string> { "find_key", "replace_value" }, new HashSet<string> { "find_key", "replace_value", "item_type", "item_name", "file_path" })
        };

        public Parameter(string repositoryDirectory, List<string> itemTypeInScope, string environment, string parameterFileName = "parameter.yml")
        {
            _repositoryDirectory = repositoryDirectory;
            _itemTypeInScope = itemTypeInScope;
            _environment = environment;
            _parameterFileName = parameterFileName;
            _parameterFilePath = Path.Combine(repositoryDirectory, parameterFileName);

            RefreshParameterFile();
        }

        private void RefreshParameterFile()
        {
            EnvironmentParameter.Clear();

            if (!File.Exists(_parameterFilePath)) return;

            if (!ValidateAndLoadParameters(out var parameterDict)) return;

            EnvironmentParameter = parameterDict;
        }

        private bool ValidateAndLoadParameters(out Dictionary<string, dynamic> parameterDict)
        {
            parameterDict = new();
            try
            {
                var yamlContent = File.ReadAllText(_parameterFilePath);
                yamlContent = Utils.ReplaceVariablesInParameterFile(yamlContent);

                var validationErrors = ValidateYamlContent(yamlContent);
                if (validationErrors.Count > 0)
                {
                    LoadErrorMessage = ParameterMessages.InvalidYaml[validationErrors.First()];
                    return false;
                }

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var obj = deserializer.Deserialize<Dictionary<string, dynamic>>(yamlContent);
                parameterDict = obj;
                Logging.LogDebug(ParameterMessages.ParameterMsgs["content is valid"]);
                return true;
            }
            catch (YamlDotNet.Core.YamlException e)
            {
                LoadErrorMessage = ParameterMessages.InvalidYaml[e.Message];
                return false;
            }
        }

        private List<string> ValidateYamlContent(string content)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(content))
            {
                errors.Add("YAML content is empty");
                return errors;
            }

            // UTF-8 regex validation (simplified version for .NET)
            var utf8Pattern = new Regex(@"^[\u0000-\uFFFF]+$", RegexOptions.Compiled);
            if (!utf8Pattern.IsMatch(content))
            {
                errors.Add(ParameterMessages.InvalidYaml["char"]);
            }

            return errors;
        }

        public bool ValidateParameterFile()
        {
            if (!EnvironmentParameter.Any())
            {
                if (!File.Exists(_parameterFilePath))
                {
                    Logging.LogWarning(ParameterMessages.ParameterMsgs["not found"]);
                    return false;
                }
                return false;
            }

            return true;
        }
    }
}
