using System.Net;
using Core.Domain.Exceptions.Common;
using Core.Domain.Models.Api;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Core.Web.Middlewares;

public class GlobalExceptionHandlerMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            if (e is ApiExceptionBase apiException)
            {
                context.Response.StatusCode = apiException.StatusCode;
                await context.Response.WriteAsJsonAsync(
                    ResponseModel.ResultFromException(e, (HttpStatusCode)apiException.StatusCode));
                Log.Error(e, "ApiException: {Message}", e.Message);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await context.Response.WriteAsJsonAsync(
                    ResponseModel.ResultFromException(e));
                Log.Fatal(e, "Unhandled Exception: {Message}", e.Message);
            }
        }
    }
}
