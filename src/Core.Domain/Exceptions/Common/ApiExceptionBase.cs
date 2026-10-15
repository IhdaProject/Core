using Core.Domain.Enums;

namespace Core.Domain.Exceptions.Common;

public class ApiExceptionBase : Exception
{
    public virtual int StatusCode { get; set; } = 500;
    public virtual ErrorTypes ErrorCode { get; set; } = ErrorTypes.ServerInternalError;

    public ApiExceptionBase(string message) : base(message)
    {
    }

    public ApiExceptionBase(string message, Exception? innerException) : base(message, innerException)
    {
    }

    public ApiExceptionBase(Exception exception) : base(exception.Message, exception)
    {
        StatusCode = 500;
        ErrorCode = ErrorTypes.ServerInternalError;
    }
}
