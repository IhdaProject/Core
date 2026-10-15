using Core.Domain.Exceptions;
using Core.Domain.Exceptions.Common;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Grpc.Interceptors;

/// <summary>
/// Serverda gRPC chaqiruvlari paytida yuz bergan domen istisnolari (<see cref="ApiExceptionBase"/>)
/// ni tegishli <see cref="RpcException"/> va gRPC holat kodlariga (StatusCode) o'giruvchi interceptor.
/// </summary>
public class GrpcExceptionInterceptor : Interceptor
{
    private readonly ILogger<GrpcExceptionInterceptor> _logger;

    /// <summary>
    /// <see cref="GrpcExceptionInterceptor"/> konstruktori.
    /// </summary>
    /// <param name="logger">Tizim logeri.</param>
    public GrpcExceptionInterceptor(ILogger<GrpcExceptionInterceptor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("gRPC NotFound: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("gRPC Validation: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (AlreadyExistsException ex)
        {
            _logger.LogWarning("gRPC AlreadyExists: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.AlreadyExists, ex.Message));
        }
        catch (ForbiddenException ex)
        {
            _logger.LogWarning("gRPC PermissionDenied: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (UnauthorizedException ex)
        {
            _logger.LogWarning("gRPC Unauthenticated: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
        catch (ApiExceptionBase ex)
        {
            _logger.LogWarning("gRPC ApiException: {Message} (Code: {Code})", ex.Message, ex.StatusCode);
            var status = ex.StatusCode switch
            {
                400 => StatusCode.InvalidArgument,
                401 => StatusCode.Unauthenticated,
                403 => StatusCode.PermissionDenied,
                404 => StatusCode.NotFound,
                409 => StatusCode.AlreadyExists,
                _ => StatusCode.Internal
            };
            throw new RpcException(new Status(status, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC Serverda kutilmagan xatolik yuz berdi");
            throw new RpcException(new Status(StatusCode.Internal, "Serverda ichki xatolik yuz berdi."));
        }
    }
}
