using Core.Domain.Exceptions;
using Core.Domain.Models.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Core.Web.Filters;

public class ModelValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ModelState.IsValid)
        {
            await next();
            return;
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Result =
            new JsonResult(ResponseModel.ResultFromModelState(context.ModelState, new ValidationException("Model validation failed.")));
    }
}
