using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;
//createe a base controller for all API controllers to inherit from, providing common functionality
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The logged-in user's id from the server-side session (0 if none).</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    protected ObjectResult NotFoundProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status404NotFound, title: "Not Found");

    protected ObjectResult ForbiddenProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status409Conflict, title: "Conflict");

    protected ObjectResult BadRequestProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
}
