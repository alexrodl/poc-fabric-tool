namespace FabricTool
{
    public static class Constants
    {
        // General
        public const string Version = "0.1.21";
        public const string DefaultWorkspaceId = "00000000-0000-0000-0000-000000000000";
        public const string DefaultApiRootUrl = "https://api.powerbi.com";
        public const string FabricApiRootUrl = "https://api.fabric.microsoft.com";
        public static readonly HashSet<string> FeatureFlag = new();
        public static string UserAgent => $"ms-fabric-cicd/{Version}";

        // Item Type
        public static readonly string[] AcceptedItemTypesUpn =
        [
            "DataPipeline", "Environment", "Notebook", "Report", "SemanticModel",
            "Lakehouse", "MirroredDatabase", "VariableLibrary", "CopyJob", "Eventhouse",
            "KQLDatabase", "KQLQueryset", "Reflex", "Eventstream", "Warehouse",
            "SQLDatabase", "KQLDashboard", "Dataflow"
        ];

        public static readonly string[] AcceptedItemTypesNonUpn = AcceptedItemTypesUpn;

        // Publish
        public static readonly Dictionary<string, int> MaxRetryOverride = new()
        {
            ["SemanticModel"] = 10,
            ["Report"] = 10,
            ["Eventstream"] = 10,
            ["KQLDatabase"] = 10,
            ["SQLDatabase"] = 10,
            ["Warehouse"] = 10,
            ["Dataflow"] = 10,
            ["VariableLibrary"] = 7
        };

        public static readonly string[] ShellOnlyPublish =
        [
            "Environment", "Lakehouse", "Warehouse", "SQLDatabase"
        ];

        // Regex Constants
        public const string ValidGuidRegex = @"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$";
        public const string WorkspaceIdReferenceRegex = @"\""?(default_lakehouse_workspace_id|workspaceId|workspace)\""?\s*[:=]\s*\""(.*?)\""";
        public const string DataflowIdReferenceRegex = @"(dataflowId)\s*=\s*""(.*?)""";
        public const string InvalidFolderCharRegex = @"[~""#.%&*:<>?/\\{|}]";

        // Item Type to File Mapping
        public static readonly Dictionary<string, string> ItemTypeToFile = new()
        {
            ["DataPipeline"] = "pipeline-content.json",
            ["Dataflow"] = "mashup.pq"
        };

        // Data Pipeline Activities
        public static readonly Dictionary<string, string[]> DataPipelineActivityTypes = new()
        {
            ["RefreshDataflow"] = ["workspaceId", "Dataflow", "dataflowId", "dataflows"],
            ["PBISemanticModelRefresh"] = ["groupId", "SemanticModel", "datasetId", "semanticModels"]
        };

        // Parameter file config
        public const string ParameterFileName = "parameter.yml";
        public static readonly string[] ItemAttrLookup = ["id", "sqlendpoint"];

        // Validation messages
        public static class ParameterMessages
        {
            public static readonly Dictionary<string, string> InvalidYaml = new()
            {
                ["char"] = "Invalid characters found",
                ["quote"] = "Unclosed quote: {0}"
            };

            public static readonly Dictionary<string, string> InvalidReplaceValueSparkPool = new()
            {
                ["missing key"] = "The '{0}' environment dict in spark_pool must contain a 'type' and a 'name' key",
                ["missing value"] = "The '{0}' environment in spark_pool is missing a value for '{1}' key",
                ["invalid value"] = "The '{0}' environment in spark_pool must contain 'Capacity' or 'Workspace' as a value for 'type'"
            };

            public static readonly Dictionary<string, string> ParameterMsgs = new()
            {
                ["validating"] = "Validating {0}",
                ["passed"] = "Validation passed: {0}",
                ["failed"] = "Validation failed with error: {0}",
                ["terminate"] = "Validation terminated: {0}",
                ["found"] = string.Format("Found {0} file", ParameterFileName),
                ["not found"] = "Parameter file not found with path: {0}",
                ["invalid content"] = InvalidYaml["char"], // Fallback example
                ["valid load"] = string.Format("Successfully loaded {0}", ParameterFileName),
                ["invalid load"] = string.Format("Error loading {0} {{0}}", ParameterFileName),
                ["invalid structure"] = "Invalid parameter file structure",
                ["valid structure"] = "Parameter file structure is valid",
                ["invalid name"] = "Invalid parameter name '{0}' found in the parameter file",
                ["valid name"] = "Parameter names are valid",
                ["invalid data type"] = "The provided '{0}' is not of type {1} in {2}",
                ["missing key"] = "{0} is missing keys",
                ["invalid key"] = "{0} contains invalid keys",
                ["valid keys"] = "{0} contains valid keys",
                ["missing required value"] = "Missing value for '{0}' key in {1}",
                ["valid required values"] = "Required values in {0} are valid",
                ["missing replace value"] = "{0} is missing a replace value for '{1}' environment",
                ["valid replace value"] = "Values in 'replace_value' dict in {0} are valid",
                ["invalid replace value"] = InvalidReplaceValueSparkPool["missing key"],
                ["no optional"] = "No optional values provided in {0}",
                ["invalid item type"] = "Item type '{0}' not in scope",
                ["invalid item name"] = "Item name '{0}' not found in the repository directory",
                ["invalid file path"] = "Path '{0}' not found in the repository directory",
                ["valid optional"] = "Optional values in {0} are valid",
                ["valid parameter"] = "{0} parameter is valid",
                ["skip"] = "The {0} '{1}' replacement will be skipped due to {2} in parameter {3}",
                ["no target env"] = "target environment '{0}' not found",
                ["no filter match"] = "unmatched optional filters"
            };
        }

        public const string Indent = "->";
    }
}
