using Core.Domain.Enums;
using Core.Domain.Exceptions.Common;

namespace Core.Domain.Exceptions;

public sealed class ForbiddenException : ApiExceptionBase
{
    public ForbiddenException(string message = "Forbidden", ErrorTypes errorCode = ErrorTypes.Forbidden) : base(message)
    {
        StatusCode = 403;
        ErrorCode = errorCode;
    }

    public ForbiddenException(string message, Exception innerException, ErrorTypes errorCode = ErrorTypes.Forbidden)
        : base(message, innerException)
    {
        StatusCode = 403;
        ErrorCode = errorCode;
    }
}
