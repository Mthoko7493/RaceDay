using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Security;

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

/// <summary>
/// Session-based authorisation. With no roles listed, any logged-in user is allowed.
/// 401 = no active session, 403 = logged in but the role is not allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SessionAuthorizeAttribute(params string[] roles) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || role is null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "You must be logged in to access this resource."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        if (roles.Length > 0 && !roles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"This action is restricted to: {string.Join(", ", roles)}."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
