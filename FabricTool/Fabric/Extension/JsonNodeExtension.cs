using System.Text.Json.Nodes;

namespace FabricTool.Fabric.Extension
{
    public static class JsonNodeExtensions
    {
        public static IEnumerable<JsonObject> DescendantsAndSelf(this JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                yield return obj;

                foreach (var property in obj)
                {
                    foreach (var child in DescendantsAndSelf(property.Value))
                    {
                        yield return child;
                    }
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    foreach (var child in DescendantsAndSelf(item))
                    {
                        yield return child;
                    }
                }
            }
        }
    }

}
