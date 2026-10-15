using Core.Domain.Enums;
using Core.Domain.Exceptions.Common;

namespace Core.Domain.Exceptions;

public sealed class AlreadyExistsException : ApiExceptionBase
{
    public AlreadyExistsException(string message, ErrorTypes errorCode = ErrorTypes.ServerInternalError) : base(message)
    {
        StatusCode = 403;
        ErrorCode = errorCode;
    }
}
