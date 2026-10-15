namespace Core.Domain.Enums;

public enum ErrorTypes
{
    ValidationError = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    Conflict = 409,
    AlreadyExists = 409,
    ServerInternalError = 500,
    ServerIntervalError = 500 // Preserved for backward compatibility
}
