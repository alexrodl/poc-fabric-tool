namespace FabricTool.Common;

public static class Logging
{
    private static readonly bool IsAzureDevOps = Environment.GetEnvironmentVariable("TF_BUILD") == "True";

    public static void LogInformation(string message, params object[] args)
    {
        LogInfo(string.Format(message, args));
    }
    public static void LogInfo(string message)
    {
        Write(message, ConsoleColor.White, "info", isError: false);
    }

    public static void LogWarning(string message)
    {
        Write(message, ConsoleColor.Yellow, "warn", isWarning: true);
    }

    public static void LogError(string message)
    {
        Write(message, ConsoleColor.Red, "error", isError: true);
    }

    public static void LogDebug(string message)
    {
        if (!IsAzureDevOps)
            Write($"[DEBUG] {message}", ConsoleColor.Gray, "debug");
    }

    public static void PrintHeader(string message)
    {
        var line = new string('#', 100);
        var formatted = $"########## {message} ";
        formatted += line.Substring(formatted.Length + 1 > line.Length ? 0 : formatted.Length + 1);

        if (IsAzureDevOps)
        {
            Console.WriteLine($"##[section]{message}");
        }
        else
        {
            Console.WriteLine();
            Write(line, ConsoleColor.Green);
            Write(formatted, ConsoleColor.Green);
            Write(line, ConsoleColor.Green);
            Console.WriteLine();
        }
    }

    private static void Write(string message, ConsoleColor color, string level = "info", bool isWarning = false, bool isError = false)
    {
        if (IsAzureDevOps)
        {
            if (isWarning)
                Console.WriteLine($"##vso[task.logissue type=warning]{message}");
            else if (isError)
                Console.WriteLine($"##vso[task.logissue type=error]{message}");
            else
                Console.WriteLine(message);
        }
        else
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var levelTag = level switch
            {
                "info" => "[info]",
                "warn" => "[warn]",
                "error" => "[error]",
                "debug" => "[debug]",
                _ => "[log]"
            };
            Console.ForegroundColor = color;
            Console.WriteLine($"{levelTag.PadRight(8)} {timestamp} - {message}");
            Console.ResetColor();
        }
    }
}


