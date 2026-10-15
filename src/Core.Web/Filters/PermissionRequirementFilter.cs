using System.Security.Claims;
using Core.Domain.Enums;
using Core.Domain.Exceptions;
using Core.Web.Constants;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Core.Web.Filters;

public class PermissionRequirementFilter(int[] requiredPermissionsCodes) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var rawStructuresId = context.HttpContext.User.FindFirstValue(CustomClaimNames.Structures)
                              ?? throw new UnauthorizedException("Unauthorized");

        if (string.IsNullOrEmpty(rawStructuresId))
            throw new UnauthorizedException("Unauthorized");

        var permissions = rawStructuresId.Split(',').SelectMany(x =>
        {
            if (long.TryParse(x, out var structureId) &&
                StaticCache.Permissions.TryGetValue(structureId, out var structurePermissions) &&
                structurePermissions is { Count: > 0 })
                return structurePermissions;
            return [];
        }).ToList();

        if (requiredPermissionsCodes.Any(x => permissions.All(pc => pc != x)))
            throw new AlreadyExistsException("Forbidden");

        return Task.CompletedTask;
    }
}
