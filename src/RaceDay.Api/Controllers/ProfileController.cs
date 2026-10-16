using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api/profile")]
[Tags("User Profile")]
[SessionAuthorize]
public class ProfileController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Get the logged-in user's own profile.</summary>
    /// <response code="200">Profile returned.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get()
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return Problem(detail: "Your account no longer exists.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
        }
        return Ok(user.ToResponse());
    }

    /// <summary>Update the logged-in user's full name and phone number.</summary>
    /// <response code="200">Updated profile returned.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return Problem(detail: "Your account no longer exists.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        await db.SaveChangesAsync();
        return Ok(user.ToResponse());
    }
}
