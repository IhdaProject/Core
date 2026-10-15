using Core.Domain.Exceptions.Common;

namespace Core.Domain.Exceptions;

public class ExpiredException(string message, Exception? innerException = null) : ApiExceptionBase(message, innerException);
