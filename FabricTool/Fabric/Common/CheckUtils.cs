using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FabricTool.Common;
public static class CheckUtils
{    
    public static async Task<Dictionary<string, List<string>>> ParseChangelogAsync(ILogger logger)
    {
        throw new NotImplementedException();
    }

    public static async Task CheckVersionAsync(string currentVersion, ILogger logger)
    {
        throw new NotImplementedException();
    }

    public static string CheckFileType(string filePath, ILogger logger)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLower();
            if (extension == ".jpg" || extension == ".png" || extension == ".gif")
                return "image";
            if (extension == ".exe" || extension == ".dll")
                return "binary";
            return "text";
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Error determining file type of {filePath}: {ex.Message}", ex);
        }
    }

    public static Regex CheckRegex(string pattern)
    {
        try
        {
            return new Regex(pattern);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid regex: {ex.Message}", ex);
        }
    }
}
