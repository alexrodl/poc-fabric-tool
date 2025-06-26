using Microsoft.Extensions.Logging;

namespace FabricTool.Common;

public class BaseCustomException : Exception
{
    public ILogger? Logger { get; }
    public string? AdditionalInfo { get; }

    public BaseCustomException(string message, ILogger? logger = null, string? additionalInfo = null)
        : base(message)
    {
        Logger = logger;
        AdditionalInfo = additionalInfo;
    }
}

public class ParsingException : BaseCustomException
{
    public ParsingException(string message, ILogger? logger = null, string? additionalInfo = null)
        : base(message, logger, additionalInfo) { }
}

public class InputError : BaseCustomException
{
    public InputError(string message, ILogger? logger = null, string? additionalInfo = null)
        : base(message, logger, additionalInfo) { }
}

public class TokenException : BaseCustomException
{
    public TokenException(string message, ILogger? logger = null, string? additionalInfo = null)
        : base(message, logger, additionalInfo) { }
}

public class InvokeException : BaseCustomException
{
    public InvokeException(string message, ILogger? logger = null, string? additionalInfo = null)
        : base(message, logger, additionalInfo) { }
}

public class ItemDependencyException : BaseCustomException
{
    public ItemDependencyException(string message, ILogger? logger = null, string? additionalInfo = null)
        : base(message, logger, additionalInfo) { }
}

public class FileTypeException : BaseCustomException
{
    public FileTypeException(string message, ILogger? logger = null, string? additionalInfo = null)
        : base(message, logger, additionalInfo) { }
}

public class ParameterFileException : BaseCustomException
{
    public ParameterFileException(string message, ILogger? logger = null, string? additionalInfo = null)
        : base(message, logger, additionalInfo) { }
}

public class FailedPublishedItemStatusException : BaseCustomException
{
    public FailedPublishedItemStatusException(string message, ILogger? logger = null, string? additionalInfo = null)
        : base(message, logger, additionalInfo) { }
}

