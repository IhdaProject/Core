using Core.Domain.Enums;
using Core.Domain.Exceptions.Common;

namespace Core.Domain.Exceptions;

public sealed class ValidationException : ApiExceptionBase
{
    public ValidationException(string message, ErrorTypes errorCode = ErrorTypes.ServerInternalError) : base(message)
    {
        StatusCode = 400;
        ErrorCode = errorCode;
    }
}
