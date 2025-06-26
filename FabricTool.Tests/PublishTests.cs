using FabricTool;

namespace FabricTool.Tests;

public class PublishTests
{
    [Fact]
    public void PublishAllRuns()
    {
        var ws = new FabricWorkspace("id", "/repo", new[] { "Notebook", "Report" });
        Publish.PublishAllItems(ws);
    }
}
