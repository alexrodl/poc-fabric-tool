using System.CommandLine;
using FabricTool;

var root = new RootCommand("Fabric Tool");

var workspaceOption = new Option<string>("--workspace-name", description: "Workspace Name") { IsRequired = true };
var repoOption = new Option<string>("--repo", description: "Repository directory") { IsRequired = true };
//var itemOption = new Option<string[]>("--items", description: "Item types", getDefaultValue: () => new string[0]);

var publishCmd = new Command("publish-all", "Publish all items");
publishCmd.AddOption(workspaceOption);
publishCmd.AddOption(repoOption);
//publishCmd.AddOption(itemOption);
publishCmd.SetHandler((string workspaceName, string repo) =>
{
    var ws = new FabricWorkspace(workspaceName, repo);
    Publish.PublishAllItemsAsync(ws);
}, workspaceOption, repoOption);

//var unpublishCmd = new Command("unpublish-orphan", "Unpublish orphaned items");
//unpublishCmd.AddOption(workspaceOption);
//unpublishCmd.AddOption(repoOption);
//unpublishCmd.AddOption(itemOption);
//unpublishCmd.SetHandler((string workspaceId, string repo, string[] items) =>
//{
//    var ws = new FabricWorkspace(workspaceId, repo, items);
//    Publish.UnpublishAllOrphanItems(ws);
//}, workspaceOption, repoOption, itemOption);

root.AddCommand(publishCmd);
//root.AddCommand(unpublishCmd);

return await root.InvokeAsync(args);
