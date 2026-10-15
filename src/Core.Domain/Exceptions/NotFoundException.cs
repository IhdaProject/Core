using System.Diagnostics.CodeAnalysis;
using Core.Domain.Enums;
using Core.Domain.Exceptions.Common;

namespace Core.Domain.Exceptions;

public sealed class NotFoundException : ApiExceptionBase
{
    public NotFoundException(string message, ErrorTypes errorCode = ErrorTypes.ServerInternalError) : base(message)
    {
        StatusCode = 404;
        ErrorCode = errorCode;
    }

    public static void ThrowIfNull([NotNull] object? data, string message = "Not found", ErrorTypes errorCode = ErrorTypes.ServerInternalError)
    {
        if (data is null) throw new NotFoundException(message, errorCode);
    }
}
