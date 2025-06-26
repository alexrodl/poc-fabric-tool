using Azure.Core;
using System.Text.RegularExpressions;

namespace FabricTool.Common;

public static class ValidateInput
{
    public static T ValidateDataType<T>(string variableName, object inputValue)
    {
        if (inputValue is not T)
        {
            var msg = $"The provided {variableName} is not of type {typeof(T).Name}.";
            throw new InputError(msg);
        }

        return (T)inputValue;
    }

    public static List<string> ValidateItemTypeInScope(IEnumerable<string> inputValue, bool upnAuth)
    {
        var list = inputValue.ToList();
        if (list.Any(s => s == null))
            throw new InputError("Item type list contains null values.");

        var accepted = upnAuth
            ? Constants.AcceptedItemTypesUpn
            : Constants.AcceptedItemTypesNonUpn;

        foreach (var itemType in list)
        {
            if (!accepted.Contains(itemType))
            {
                var msg = $"Invalid or unsupported item type: '{itemType}'. " +
                          $"For User Identity Authentication, must be one of {string.Join(", ", Constants.AcceptedItemTypesUpn)}. " +
                          $"For Service Principal or Managed Identity Authentication, " +
                          $"must be one of {string.Join(", ", Constants.AcceptedItemTypesNonUpn)}.";
                throw new InputError(msg);
            }
        }

        return list;
    }

    public static string ValidateWorkspaceId(string inputValue)
    {
        ValidateDataType<string>("workspace_id", inputValue);

        if (!Regex.IsMatch(inputValue, Constants.ValidGuidRegex))
            throw new InputError("The provided workspace_id is not a valid guid.");

        return inputValue;
    }

    public static string ValidateWorkspaceName(string inputValue)
    {
        return ValidateDataType<string>("workspace_name", inputValue);
    }

    public static string ValidateEnvironment(string inputValue)
    {
        return ValidateDataType<string>("environment", inputValue);
    }

    public static string ValidateRepositoryDirectory(string inputValue)
    {
        ValidateDataType<string>("repository_directory", inputValue);

        if (!Directory.Exists(inputValue))
            throw new InputError($"The provided repository_directory '{inputValue}' does not exist.");

        var fullPath = Path.GetFullPath(inputValue);
        if (Path.GetFullPath(inputValue) != inputValue)
        {
            Console.WriteLine($"Relative directory path '{inputValue}' resolved as '{fullPath}'");
        }

        return fullPath;
    }

    public static FabricWorkspace ValidateFabricWorkspace(FabricWorkspace inputValue)
    {
        if (inputValue == null)
            throw new InputError("FabricWorkspace object is null.");

        return inputValue;
    }

    public static TokenCredential ValidateTokenCredential(TokenCredential inputValue)
    {
        return ValidateDataType<TokenCredential>("credential", inputValue);
    }
}
