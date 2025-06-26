using FabricTool;

namespace FabricTool.Tests;

public class UnitTest1
{
    [Fact]
    public void WorkspaceInitialization()
    {
        var ws = new FabricWorkspace("id", "/repo", new[] { "Notebook" });
        Assert.Equal("id", ws.WorkspaceId);
    }
}
