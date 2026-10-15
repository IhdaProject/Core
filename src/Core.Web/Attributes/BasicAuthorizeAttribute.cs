using Core.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Core.Web.Attributes;

public class BasicAuthorizeAttribute : TypeFilterAttribute
{
    public BasicAuthorizeAttribute(params string[] users) : base(typeof(BasicAuthenticationFilter))
    {
        Arguments = [users];
    }
}
