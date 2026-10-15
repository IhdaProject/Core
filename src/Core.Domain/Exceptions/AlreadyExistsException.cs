using Core.Domain.Enums;
using Core.Domain.Exceptions.Common;

namespace Core.Domain.Exceptions;

public sealed class AlreadyExistsException : ApiExceptionBase
{
    public AlreadyExistsException(string message, ErrorTypes errorCode = ErrorTypes.Conflict) : base(message)
    {
        StatusCode = 409;
        ErrorCode = errorCode;
    }
}
