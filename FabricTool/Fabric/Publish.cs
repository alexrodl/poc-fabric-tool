using FabricTool.Common;
using System.Text.RegularExpressions;

namespace FabricTool
{
    public static class Publish
    {
        public static async Task PublishAllItemsAsync(FabricWorkspace fabricWorkspace, string? itemNameExcludeRegex = null)
        {
            fabricWorkspace = ValidateInput.ValidateFabricWorkspace(fabricWorkspace);

            if (!Constants.FeatureFlag.Contains("disable_workspace_folder_publish"))
            {
                await fabricWorkspace.RefreshDeployedFoldersAsync();
                await fabricWorkspace.RefreshRepositoryFoldersAsync();
                await fabricWorkspace.PublishFoldersAsync();
            }

            await fabricWorkspace.RefreshDeployedItemsAsync();
            await fabricWorkspace.RefreshRepositoryItemsAsync();

            if (!string.IsNullOrEmpty(itemNameExcludeRegex))
            {
                Logging.LogWarning(
                    "Using item_name_exclude_regex is risky as it can prevent needed dependencies from being deployed. Use at your own risk.");
                fabricWorkspace.PublishItemNameExcludeRegex = itemNameExcludeRegex;
            }

            foreach (var itemType in fabricWorkspace.ItemTypesInScope)
            {
                switch (itemType)
                {
                    case "VariableLibrary":
                        Logging.PrintHeader("Publishing Variable Libraries");
                        //await ItemsPublisher.PublishVariableLibrariesAsync(fabricWorkspace);
                        break;
                    case "Warehouse":
                        Logging.PrintHeader("Publishing Warehouses");
                        //await ItemsPublisher.PublishWarehousesAsync(fabricWorkspace);
                        break;
                    case "Lakehouse":
                        Logging.PrintHeader("Publishing Lakehouses");
                        //await ItemsPublisher.PublishLakehousesAsync(fabricWorkspace);
                        break;
                    case "SQLDatabase":
                        Logging.PrintHeader("Publishing SQL Databases");
                        //await ItemsPublisher.PublishSQLDatabasesAsync(fabricWorkspace);
                        break;
                    case "MirroredDatabase":
                        Logging.PrintHeader("Publishing MirroredDatabase");
                        //await ItemsPublisher.PublishMirroredDatabaseAsync(fabricWorkspace);
                        break;
                    case "Environment":
                        Logging.PrintHeader("Publishing Environments");
                        //await ItemsPublisher.PublishEnvironmentsAsync(fabricWorkspace);
                        break;
                    case "Notebook":
                        Logging.PrintHeader("Publishing Notebooks");
                        await Notebook.PublishNotebooksAsync(fabricWorkspace);
                        break;
                    case "SemanticModel":
                        Logging.PrintHeader("Publishing SemanticModels");
                        //await ItemsPublisher.PublishSemanticModelsAsync(fabricWorkspace);
                        break;
                    case "Report":
                        Logging.PrintHeader("Publishing Reports");
                        //await ItemsPublisher.PublishReportsAsync(fabricWorkspace);
                        break;
                    case "CopyJob":
                        Logging.PrintHeader("Publishing CopyJobs");
                        //await ItemsPublisher.PublishCopyJobsAsync(fabricWorkspace);
                        break;
                    case "Eventhouse":
                        Logging.PrintHeader("Publishing Eventhouses");
                        //await ItemsPublisher.PublishEventhousesAsync(fabricWorkspace);
                        break;
                    case "KQLDatabase":
                        Logging.PrintHeader("Publishing KQL Databases");
                        //await ItemsPublisher.PublishKQLDatabasesAsync(fabricWorkspace);
                        break;
                    case "KQLQueryset":
                        Logging.PrintHeader("Publishing KQL Querysets");
                        //await ItemsPublisher.PublishKQLQuerysetsAsync(fabricWorkspace);
                        break;
                    case "Reflex":
                        Logging.PrintHeader("Publishing Activators");
                        //await ItemsPublisher.PublishActivatorsAsync(fabricWorkspace);
                        break;
                    case "Eventstream":
                        Logging.PrintHeader("Publishing Eventstreams");
                        //await ItemsPublisher.PublishEventstreamsAsync(fabricWorkspace);
                        break;
                    case "KQLDashboard":
                        Logging.PrintHeader("Publishing KQLDashboard");
                        //await ItemsPublisher.PublishKQLDashboardAsync(fabricWorkspace);
                        break;
                    case "Dataflow":
                        Logging.PrintHeader("Publishing Dataflows");
                        // ItemsPublisher.PublishDataflowsAsync(fabricWorkspace);
                        break;
                    case "DataPipeline":
                        Logging.PrintHeader("Publishing DataPipelines");
                        //await ItemsPublisher.PublishDataPipelinesAsync(fabricWorkspace);
                        break;
                }
            }

            if (fabricWorkspace.ItemTypesInScope.Contains("Environment"))
            {
                Logging.PrintHeader("Checking Environment Publish State");
                //await ItemsPublisher.CheckEnvironmentPublishStateAsync(fabricWorkspace);
            }
        }

        public static async Task UnpublishAllOrphanItemsAsync(FabricWorkspace fabricWorkspace, string itemNameExcludeRegex = "^$")
        {
            fabricWorkspace = ValidateInput.ValidateFabricWorkspace(fabricWorkspace);

            Regex regex = CheckUtils.CheckRegex(itemNameExcludeRegex);

            await fabricWorkspace.RefreshDeployedItemsAsync();
            await fabricWorkspace.RefreshRepositoryItemsAsync();
            Logging.PrintHeader("Unpublishing Orphaned Items");

            var unpublishFlagMapping = new Dictionary<string, string>
            {
                { "Lakehouse", "enable_lakehouse_unpublish" },
                { "SQLDatabase", "enable_sqldatabase_unpublish" },
                { "Warehouse", "enable_warehouse_unpublish" }
            };

            var orderedTypes = new List<string>
            {
                "DataPipeline", "Dataflow", "Eventstream", "Reflex", "KQLDashboard", "KQLQueryset",
                "KQLDatabase", "Eventhouse", "CopyJob", "Report", "SemanticModel", "Notebook", "Environment",
                "MirroredDatabase", "SQLDatabase", "Lakehouse", "Warehouse", "VariableLibrary"
            };

            foreach (var itemType in orderedTypes)
            {
                if (!fabricWorkspace.ItemTypesInScope.Contains(itemType)) continue;

                var flag = unpublishFlagMapping.GetValueOrDefault(itemType);
                if (flag != null && !Constants.FeatureFlag.Contains(flag)) continue;

                var deployedNames = fabricWorkspace.DeployedItems.TryGetValue(itemType, out var deployed) ? deployed.Keys.ToHashSet() : new();
                var repositoryNames = fabricWorkspace.RepositoryItems.TryGetValue(itemType, out var repo) ? repo.Keys.ToHashSet() : new();

                var toDelete = deployedNames.Except(repositoryNames).Where(name => !regex.IsMatch(name)).ToList();

                if (itemType is "Dataflow" or "DataPipeline")
                {
                    //var findRefs = itemType == "DataPipeline"
                    //    ? ItemsPublisher.FindReferencedDataPipelines
                    //: ItemsPublisher.FindReferencedDataflows;

                    //toDelete = ItemsPublisher.SetUnpublishOrder(fabricWorkspace, itemType, toDelete, findRefs);
                }

                foreach (var name in toDelete)
                {
                    await fabricWorkspace.UnpublishItemAsync(itemType, name);
                }
            }

            await fabricWorkspace.RefreshDeployedItemsAsync();
            await fabricWorkspace.RefreshDeployedFoldersAsync();

            if (!Constants.FeatureFlag.Contains("disable_workspace_folder_publish"))
            {
                await fabricWorkspace.UnpublishFoldersAsync();
            }
        }
    }
}
