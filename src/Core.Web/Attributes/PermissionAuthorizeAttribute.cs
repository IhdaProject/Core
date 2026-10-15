using Core.Domain.Enums;
using Core.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Core.Web.Attributes;

public class PermissionAuthorizeAttribute : TypeFilterAttribute
{
    public PermissionAuthorizeAttribute(params UserPermissions[] permissions) : base(typeof(PermissionRequirementFilter))
    {
        Arguments =
        [
            permissions.Cast<int>().ToArray()
        ];
    }
}
