using FabricTool.Fabric.Extension;
using System.Text.Json.Nodes;

namespace FabricTool.Common;

/// <summary>
/// Represents a deployable item in the workspace.
/// </summary>
public class Item
{
    public string Type { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Guid { get; set; }
    public string LogicalId { get; set; } = string.Empty;
    public DirectoryInfo Path { get; set; } = new DirectoryInfo(".");
    public List<FileItem> ItemFiles { get; private set; } = new();
    public string FolderId { get; set; } = string.Empty;
    public bool SkipPublish { get; set; } = false;

    private static readonly HashSet<string> ImmutableFields = new() { "Type", "Name", "Description" };

    public Item(string type, string name, string description, string guid, string logicalId = "", DirectoryInfo? path = null, string folderId = "")
    {
        Type = type;
        Name = name;
        Description = description;
        Guid = guid;
        LogicalId = logicalId;
        Path = path ?? new DirectoryInfo(".");
        FolderId = folderId;
    }

    public void SetField(string key, object value)
    {
        if (ImmutableFields.Contains(key) && GetType().GetProperty(key)?.GetValue(this) != null)
            throw new InvalidOperationException($"item {key} is immutable");

        var prop = GetType().GetProperty(key);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(this, value);
        }
    }

    public void CollectItemFiles()
    {
        ItemFiles.Clear();
        foreach (var filePath in Directory.GetFiles(Path.FullName, "*", SearchOption.AllDirectories))
        {
            ItemFiles.Add(new FileItem(Path.FullName, filePath));
        }
    }

    public void ReplaceInBody(string key, string value)
    {
        foreach (var itemFile in ItemFiles)
        {
            if (itemFile.JsonBody == null)
                continue;

            var nodes = itemFile.JsonBody.DescendantsAndSelf().OfType<JsonObject>();
            foreach (var obj in nodes)
            {
                if (obj.ContainsKey(key))
                {
                    obj[key] = value;
                }
            }
        }
    }
       
    public void ReplaceAllPlaceholders(string placeholder, string value)
    {
        foreach (var file in ItemFiles)
        {
            if (file.Type == "text" && file.Contents != null)
            {
                //file.Contents = file.Contents.Replace(placeholder, value);
            }
        }
    }
    // Optionally, you can add relative path property if needed later
    // public string RelativePath => Path.GetRelativePath(Path.FullName, file.FullName).Replace("\\", "/");
}

