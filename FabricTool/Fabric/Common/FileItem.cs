using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;

namespace FabricTool.Common;
public class FileItem
{
    private readonly HashSet<string> _immutableFields = new() { "ItemPath", "FilePath" };

    public string ItemPath { get; init; }
    public string FilePath { get; init; }
    public string Type { get; private set; } = "text";
    public string Contents { get; private set; } = string.Empty;

    public JsonNode? JsonBody { get; private set; }

    public string FileName => System.IO.Path.GetFileName(FilePath);

    public string RelativePath => System.IO.Path.GetRelativePath(ItemPath, FilePath).Replace("\\", "/");

    public FileItem(string itemPath, string filePath)
    {
        ItemPath = itemPath;
        FilePath = filePath;

        var fileType = CheckFileType(filePath);

        try
        {
            if (fileType != "text")
            {
                Contents = Convert.ToBase64String(System.IO.File.ReadAllBytes(filePath));
            }
            else
            {
                Contents = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
                JsonBody = JsonNode.Parse(Contents);
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Error reading file {filePath}. Exception: {e.Message}");
        }

        Type = fileType;
    }

    public Dictionary<string, string> GetBase64Payload()
    {
        var bytes = Type == "text" ? Encoding.UTF8.GetBytes(Contents) : Convert.FromBase64String(Contents);

        return new Dictionary<string, string>
        {
            { "path", RelativePath },
            { "payload", Convert.ToBase64String(bytes) },
            { "payloadType", "InlineBase64" }
        };
    }

    private string CheckFileType(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLower();
        return ext is ".json" or ".txt" ? "text" : "binary";
    }
}
