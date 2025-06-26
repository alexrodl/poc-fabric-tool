using FabricTool;
using FabricTool.Common;
using Microsoft.Extensions.Logging;

public static class Notebook
{
    public static async Task PublishNotebooksAsync(FabricWorkspace fabricWorkspace)
    {
        const string itemType = "Notebook";

        if (fabricWorkspace.RepositoryItems.TryGetValue(itemType, out var notebooks))
        {
            foreach (var item in notebooks.Values)
            {
                await fabricWorkspace.PublishItemAsync(item);
            }
        }
        else
        {
            Logging.LogDebug("No notebook items found in the repository.");
        }
    }
}
