using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;
//create a controller for authentication with endpoints for register, login, and logout
[Route("api/auth")]
[Tags("Authentication")]
public class AuthController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Register a new Organiser or Participant account.</summary>
    /// <remarks>The password is hashed (PBKDF2) before storage and is never returned.</remarks>
    /// <response code="201">User created.</response>
    /// <response code="400">Validation failed (e.g. invalid role or short password).</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var roleName = RoleNames.Normalize(request.Role);
        if (roleName is null)
        {
            ModelState.AddModelError(nameof(request.Role), "Role must be either 'Organiser' or 'Participant'.");
            return ValidationProblem(ModelState);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return ConflictProblem("An account with this email address already exists.");
        }

        var role = await db.Roles.FirstAsync(r => r.RoleName == roleName);
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            RoleID = role.RoleID,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            CreatedAt = DateTime.Now
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        user.Role = role;
        return Created("/api/profile", user.ToResponse());
    }

    /// <summary>Log in and start a server-side session holding the user id and role.</summary>
    /// <response code="200">Logged in; user id and role returned. A session cookie is set.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Problem(detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserID);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.RoleName);

        return Ok(user.ToResponse());
    }

    /// <summary>End the current session.</summary>
    /// <response code="200">Session cleared.</response>
    /// <response code="401">No active session.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Logged out." });
    }
}
