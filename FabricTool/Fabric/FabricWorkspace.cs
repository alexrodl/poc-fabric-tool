using Azure.Core;
using Azure.Identity;
using FabricTool.Common;
using FabricTool.Fabric.Parameter;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FabricTool;

public class FabricWorkspace
{
    public string WorkspaceId { get; private set; }
    public string RepositoryDirectory { get; private set; }
    public List<string> ItemTypesInScope { get; private set; }
    public string Environment { get; private set; }
    public string? PublishItemNameExcludeRegex { get; set; }
    public string BaseApiUrl { get; private set; }

    public FabricEndpoint Endpoint { get; private set; }
    public Dictionary<string, Dictionary<string, Item>> RepositoryItems { get; private set; } = new();
    public Dictionary<string, Dictionary<string, Item>> DeployedItems { get; private set; } = new();
    public Dictionary<string, string> RepositoryFolders { get; private set; } = new();
    public Dictionary<string, string> DeployedFolders { get; private set; } = new();
    public Dictionary<string, Dictionary<string, Dictionary<string, string>>> WorkspaceItems { get; private set; } = new();
    public Dictionary<string, object> EnvironmentParameter { get; private set; } = new();

    public FabricWorkspace(string workspaceName, string repositoryDirectory)
    {
        string? workspaceId = null;
        List<string> itemTypesInScope = null;
        string environment = "N/A";
        TokenCredential? tokenCredential = null;
        Dictionary<string, string>? kwargs = null;

        Endpoint = new FabricEndpoint(tokenCredential ?? new DefaultAzureCredential());

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            WorkspaceId = ValidateInput.ValidateWorkspaceId(workspaceId);
        }
        else if (!string.IsNullOrWhiteSpace(workspaceName))
        {
            WorkspaceId = ResolveWorkspaceIdAsync(ValidateInput.ValidateWorkspaceName(workspaceName)).GetAwaiter().GetResult();
        }
        else
        {
            throw new InputError("Either workspace_name or workspace_id must be specified.");
        }

        RepositoryDirectory = ValidateInput.ValidateRepositoryDirectory(repositoryDirectory);
        ItemTypesInScope = ValidateInput.ValidateItemTypeInScope(itemTypesInScope, true);
        Environment = ValidateInput.ValidateEnvironment(environment);

        BaseApiUrl = kwargs != null && kwargs.ContainsKey("base_api_url")
            ? $"{kwargs["base_api_url"]}/v1/workspaces/{WorkspaceId}"
            : $"{Constants.DefaultApiRootUrl}/v1/workspaces/{WorkspaceId}";

        RefreshParameterFile();
    }

    private async Task<string> ResolveWorkspaceIdAsync(string workspaceName)
    {
        var response = await Endpoint.InvokeAsync(HttpMethod.Get, $"{Constants.DefaultApiRootUrl}/v1/workspaces");

        using var doc = JsonDocument.Parse(response.Body.ToJsonString());
        var root = doc.RootElement;
        foreach (var workspace in root.GetProperty("value").EnumerateArray())
        {
            if (workspace.GetProperty("displayName").GetString() == workspaceName)
                return workspace.GetProperty("id").GetString()!;
        }
        throw new InputError($"Workspace ID could not be resolved from workspace name: {workspaceName}.");
    }

    private void RefreshParameterFile()
    {
        Logging.PrintHeader("Validating Parameter File");
        var parameter = new Parameter(RepositoryDirectory, ItemTypesInScope, Environment);
        if (parameter.ValidateParameterFile())
        {
            EnvironmentParameter = parameter.EnvironmentParameter;
        }
        else
        {
            throw new ParameterFileException("Deployment terminated due to an invalid parameter file");
        }
    }

    public async Task RefreshRepositoryItemsAsync()
    {
        RepositoryItems.Clear();
        foreach (var dir in Directory.GetDirectories(RepositoryDirectory, "*", SearchOption.AllDirectories))
        {
            var platformFile = Path.Combine(dir, ".platform");
            if (!File.Exists(platformFile)) continue;

            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Logging.LogWarning($"Directory {new DirectoryInfo(dir).Name} is empty.");
                continue;
            }

            JsonDocument itemMetadata;
            try
            {
                using var stream = File.OpenRead(platformFile);
                itemMetadata = JsonDocument.Parse(stream);
            }
            catch (FileNotFoundException ex)
            {
                throw new ParsingException($"{platformFile} path does not exist in the specified repository. {ex.Message}");
            }
            catch (JsonException ex)
            {
                throw new ParsingException($"Error decoding JSON in {platformFile}. {ex.Message}");
            }

            var metadata = itemMetadata.RootElement.GetProperty("metadata");
            if (!metadata.TryGetProperty("type", out var typeElem) || !metadata.TryGetProperty("displayName", out var nameElem))
                throw new ParsingException($"displayName & type are required in {platformFile}");

            var type = typeElem.GetString()!;
            var name = nameElem.GetString()!;
            var description = metadata.TryGetProperty("description", out var descElem) ? descElem.GetString() ?? string.Empty : string.Empty;
            var logicalId = itemMetadata.RootElement.GetProperty("config").GetProperty("logicalId").GetString()!;

            var relativePath = Path.GetRelativePath(RepositoryDirectory, dir).Replace("\\", "/");
            var relativeParentPath = Path.GetDirectoryName(relativePath)?.Replace("\\", "/") ?? "";
            var folderId = Constants.FeatureFlag.Contains("disable_workspace_folder_publish") ? "" : RepositoryFolders.GetValueOrDefault(relativeParentPath, "");

            var guid = DeployedItems.GetValueOrDefault(type)?.GetValueOrDefault(name)?.Guid ?? "";

            var item = new Item(type, name, description, guid, logicalId, new DirectoryInfo(dir), folderId);
            item.CollectItemFiles();

            if (!RepositoryItems.ContainsKey(type)) RepositoryItems[type] = new();
            RepositoryItems[type][name] = item;
        }
    }

    public async Task RefreshDeployedItemsAsync()
    {
        var response = await Endpoint.InvokeAsync(HttpMethod.Get, $"{BaseApiUrl}/items");
        DeployedItems.Clear();
        WorkspaceItems.Clear();

        using var doc = JsonDocument.Parse(response.Body.ToJsonString());
        var root = doc.RootElement;

        foreach (var item in root.GetProperty("value").EnumerateArray())
        {
            var type = item.GetProperty("type").GetString()!;
            var name = item.GetProperty("displayName").GetString()!;
            var guid = item.GetProperty("id").GetString()!;
            var description = item.GetProperty("description").GetString() ?? string.Empty;
            var folderId = item.TryGetProperty("folderId", out var fId) ? fId.GetString() ?? string.Empty : string.Empty;
            var sqlEndpoint = "";

            if (!DeployedItems.ContainsKey(type)) DeployedItems[type] = new();
            if (!WorkspaceItems.ContainsKey(type)) WorkspaceItems[type] = new();

            if (type == "Lakehouse")
            {
                var lakehouseResp = await Endpoint.InvokeAsync(HttpMethod.Get, $"{BaseApiUrl}/lakehouses/{guid}");

                using var rootLakehouse = JsonDocument.Parse(response.Body.ToJsonString());

                sqlEndpoint = rootLakehouse.RootElement
                    .GetProperty("properties")
                    .GetProperty("sqlEndpointProperties")
                    .TryGetProperty("connectionString", out var connStr)
                    ? connStr.GetString() ?? string.Empty : string.Empty;

                if (string.IsNullOrEmpty(sqlEndpoint))
                {
                    Logging.LogDebug($"Failed to get SQL endpoint for Lakehouse '{name}'");
                }
            }

            var itemObj = new Item(type, name, description, guid, folderId);
            DeployedItems[type][name] = itemObj;
            WorkspaceItems[type][name] = new Dictionary<string, string>
            {
                ["id"] = guid,
                ["sqlendpoint"] = sqlEndpoint
            };
        }
    }

    public async Task UnpublishItemAsync(string itemType, string itemName)
    {
        var itemGuid = DeployedItems[itemType][itemName].Guid;
        Logging.LogInfo($"Unpublishing {itemType} '{itemName}'");
        try
        {
            await Endpoint.InvokeAsync(HttpMethod.Delete, $"{BaseApiUrl}/items/{itemGuid}");
            Logging.LogInfo($"{Constants.Indent}Unpublished");
        }
        catch (Exception ex)
        {
            Logging.LogWarning($"Failed to unpublish {itemType} '{itemName}'. Raw exception: {ex.Message}");
        }
    }

    //public async Task PublishItemAsync(Item item)
    //{
    //    Logging.LogInfo($"Publishing {item.Type} '{item.Name}'");

    //    ReplaceLogicalIds(item);
    //    ReplaceWorkspaceIds(item);
    //    ReplaceParameters(item);

    //    var response = await Endpoint.InvokeAsync(HttpMethod.Put, $"{BaseApiUrl}/items/{item.Guid}", item.GetPublishBody());
    //    item.Guid = response.Body.RootElement.GetProperty("id").GetString() ?? string.Empty;

    //    Logging.LogInfo($"{Constants.Indent}Published with ID {item.Guid}");
    //}

    public async Task PublishItemAsync(Item item)
    {
        string excludePath = "^(?!.*)";
        bool skipPublishLogging = false;
        // Skip publishing if the item is excluded by the regex
        if (!string.IsNullOrEmpty(PublishItemNameExcludeRegex))
        {
            var regexPattern = new Regex(PublishItemNameExcludeRegex);
            if (regexPattern.IsMatch(item.Name))
            {
                item.SkipPublish = true;
                Logging.LogInfo($"Skipping publishing of {item.Type} '{item.Name}' due to exclusion regex.");
                return;
            }
        }

        Logging.LogInfo($"Publishing {item.Type} '{item.Name}'");

        ReplaceLogicalIds(item);
        ReplaceWorkspaceIds(item);
        ReplaceParameters(item);

        var itemGuid = item.Guid;
        var itemFiles = item.ItemFiles;

        var maxRetries = Constants.MaxRetryOverride.ContainsKey(item.Type) ? Constants.MaxRetryOverride[item.Type] : 5;

        var metadataBody = new JsonObject
        {
            ["displayName"] = item.Name,
            ["type"] = item.Type
        };

        var shellOnlyPublish = Constants.ShellOnlyPublish.Contains(item.Type);

        JsonObject combinedBody;
        JsonObject? definitionBody = null;

        JsonObject creationPayload = null; // Alex Rodriguez - Not use creationPayload parameter
        if (creationPayload != null)
        {
            var creationObject = new JsonObject { ["creationPayload"] = creationPayload };
            combinedBody = MergeJson(metadataBody, creationObject);
        }
        else if (shellOnlyPublish)
        {
            combinedBody = metadataBody;
        }
        else
        {
            var itemPayload = new JsonArray();
            foreach (var file in itemFiles)
            {
                if (!Regex.IsMatch(file.RelativePath, excludePath))
                {
                    if (file.Type == "text" && !file.FilePath.EndsWith(".platform"))
                    {
                        //file.Contents = funcProcessFile?.Invoke(this, item, file) ?? file.Contents;
                        //file.Contents = ReplaceLogicalIds(file.Contents);
                        //file.Contents = ReplaceParameters(file, item);
                        //file.Contents = ReplaceWorkspaceIds(file.Contents);
                    }
                    // itemPayload.Add(file.Base64Payload);
                }
            }

            definitionBody = new JsonObject { ["definition"] = new JsonObject { ["parts"] = itemPayload } };
            combinedBody = MergeJson(metadataBody, definitionBody);
        }

        Logging.LogInfo($"Publishing {item.Type} '{item.Name}'");
        var isDeployed = !string.IsNullOrEmpty(itemGuid);

        if (!isDeployed)
        {
            combinedBody["folderId"] = item.FolderId;

            var response = await Endpoint.InvokeAsync(
                HttpMethod.Post,
                $"{BaseApiUrl}/items",
                combinedBody,
                null,
                maxRetries
            );

            using var doc = JsonDocument.Parse(response.Body.ToJsonString());
            var root = doc.RootElement;

            itemGuid = root.GetProperty("id").GetString() ?? throw new Exception("Item creation failed.");
            RepositoryItems[item.Type][item.Name].Guid = itemGuid;
        }
        else if (!shellOnlyPublish)
        {
            await Endpoint.InvokeAsync(
                HttpMethod.Post,
                $"{BaseApiUrl}/items/{itemGuid}/updateDefinition?updateMetadata=True",
                definitionBody!,
                null,
                maxRetries
            );
        }
        else
        {
            metadataBody.Remove("type");
            await Endpoint.InvokeAsync(
                HttpMethod.Patch,
                $"{BaseApiUrl}/items/{itemGuid}",
                metadataBody,
                null,
                maxRetries
            );
        }

        if (!Constants.FeatureFlag.Contains("disable_workspace_folder_publish"))
        {
            if (isDeployed && DeployedItems[item.Type][item.Name].FolderId != item.FolderId)
            {
                await Endpoint.InvokeAsync(
                    HttpMethod.Post,
                    $"{BaseApiUrl}/items/{itemGuid}/move",
                    new JsonObject { ["targetFolderId"] = item.FolderId },
                    null,
                    maxRetries
                );
                Logging.LogDebug($"Moved {itemGuid} from folder_id {DeployedItems[item.Type][item.Name].FolderId} to folder_id {item.FolderId}");
            }
        }

        if (!skipPublishLogging)
        {
            Logging.LogInfo($"{Constants.Indent}Published");
        }
    }

    private JsonObject MergeJson(JsonObject baseObj, JsonObject extraObj)
    {
        foreach (var kvp in extraObj)
        {
            baseObj[kvp.Key] = kvp.Value;
        }
        return baseObj;
    }

    public void ReplaceLogicalIds(Item item)
    {
        item.ReplaceInBody("logicalId", item.LogicalId);
    }

    public void ReplaceWorkspaceIds(Item item)
    {
        item.ReplaceInBody("workspaceId", WorkspaceId);
    }

    public void ReplaceParameters(Item item)
    {
        foreach (var parameter in EnvironmentParameter)
        {
            item.ReplaceAllPlaceholders(parameter.Key, parameter.Value.ToString());
        }
    }

    public async Task RefreshRepositoryFoldersAsync()
    {
        RepositoryFolders.Clear();
        var folderPath = Path.Combine(RepositoryDirectory, ".workspace_folders.json");
        if (!File.Exists(folderPath)) return;

        var json = await File.ReadAllTextAsync(folderPath);
        var doc = JsonDocument.Parse(json);

        foreach (var folder in doc.RootElement.EnumerateArray())
        {
            var path = folder.GetProperty("path").GetString() ?? "";
            var id = folder.GetProperty("id").GetString() ?? "";
            RepositoryFolders[path] = id;
        }
    }

    public async Task RefreshDeployedFoldersAsync()
    {
        DeployedFolders.Clear();
        var response = await Endpoint.InvokeAsync(HttpMethod.Get, $"{BaseApiUrl}/folders");
        using var doc = JsonDocument.Parse(response.Body.ToJsonString());
        var root = doc.RootElement;

        foreach (var folder in root.GetProperty("value").EnumerateArray())
        {
            var id = folder.GetProperty("id").GetString() ?? "";
            var path = folder.GetProperty("path").GetString() ?? "";
            DeployedFolders[path] = id;
        }
    }

    public async Task PublishFoldersAsync()
    {
        Logging.PrintHeader("Publishing Folders");
        foreach (var (path, id) in RepositoryFolders)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (DeployedFolders.ContainsKey(path)) continue;

            Logging.LogInfo($"Publishing folder: {path}");
            var payload = new { path = path };
            var response = await Endpoint.InvokeAsync(HttpMethod.Post, $"{BaseApiUrl}/folders", payload);

            using var doc = JsonDocument.Parse(response.Body.ToJsonString());
            var root = doc.RootElement;

            var newId = root.GetProperty("id").GetString() ?? string.Empty;
            DeployedFolders[path] = newId;
            Logging.LogInfo($"{Constants.Indent}Published with ID {newId}");
        }
    }

    public async Task UnpublishFoldersAsync()
    {
        Logging.PrintHeader("Unpublishing Folders");
        foreach (var (path, id) in DeployedFolders)
        {
            if (RepositoryFolders.ContainsKey(path)) continue;

            Logging.LogInfo($"Unpublishing folder: {path}");
            await Endpoint.InvokeAsync(HttpMethod.Delete, $"{BaseApiUrl}/folders/{id}");
            Logging.LogInfo($"{Constants.Indent}Unpublished");
        }
    }

    public string? ConvertIdToName(string itemType, string genericId, string lookupType)
    {
        var lookupDict = lookupType == "Repository" ? RepositoryItems : DeployedItems;

        if (lookupDict.TryGetValue(itemType, out var itemDictionary))
        {
            foreach (var itemDetails in itemDictionary.Values)
            {
                var lookupId = lookupType == "Repository" ? itemDetails.LogicalId : itemDetails.Guid;
                if (lookupId == genericId)
                {
                    return itemDetails.Name;
                }
            }
        }

        return null;
    }

    public string? ConvertPathToId(string itemType, string path)
    {
        if (RepositoryItems.TryGetValue(itemType, out var itemDictionary))
        {
            foreach (var itemDetails in itemDictionary.Values)
            {
                if (itemDetails.Path.FullName == path)
                {
                    return itemDetails.LogicalId;
                }
            }
        }

        return null;
    }

}