using Core.Domain.Enums;
using Core.Domain.Exceptions.Common;

namespace Core.Domain.Exceptions;

public class UnauthorizedException : ApiExceptionBase
{
    public override int StatusCode => 401;

    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string message, ErrorTypes errorCode = ErrorTypes.ServerInternalError) : base(message)
    {
        ErrorCode = errorCode;
    }
}
