using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;
//create 
[Tags("Categories")]
public class CategoriesController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>List all categories for an event (public).</summary>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await db.Events.AnyAsync(e => e.EventID == eventId))
            return NotFoundProblem($"Event {eventId} was not found.");

        var categories = await db.Categories.Where(c => c.EventID == eventId).OrderBy(c => c.CategoryID).ToListAsync();
        return Ok(categories.Select(c => c.ToResponse()));
    }

    /// <summary>Add an age or distance category to an event owned by the logged-in Organiser.</summary>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed (e.g. minAge greater than maxAge).</response>
    /// <response code="403">Caller is not an Organiser, or does not own the event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, [FromBody] CategoryRequest request)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventID == eventId);
        if (ev is null) return NotFoundProblem($"Event {eventId} was not found.");
        if (ev.OrganiserID != CurrentUserId) return ForbiddenProblem("You can only add categories to your own events.");
        if (request.MinAge > request.MaxAge) return BadRequestProblem("MinAge cannot be greater than MaxAge.");

        var category = new Category
        {
            EventID = eventId,
            CategoryName = request.CategoryName.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            DistanceKm = request.DistanceKm
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, category.ToResponse());
    }

    /// <summary>Update a category on an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Category updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Caller is not an Organiser, or does not own the event.</response>
    /// <response code="404">Category does not exist.</response>
    [HttpPut("api/categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request)
    {
        var category = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category is null) return NotFoundProblem($"Category {id} was not found.");
        if (category.Event.OrganiserID != CurrentUserId) return ForbiddenProblem("You can only modify categories on your own events.");
        if (request.MinAge > request.MaxAge) return BadRequestProblem("MinAge cannot be greater than MaxAge.");

        category.CategoryName = request.CategoryName.Trim();
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.DistanceKm = request.DistanceKm;
        await db.SaveChangesAsync();

        return Ok(category.ToResponse());
    }

    /// <summary>Remove a category from an event owned by the logged-in Organiser.</summary>
    /// <response code="204">Category deleted.</response>
    /// <response code="403">Caller is not an Organiser, or does not own the event.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Participants are already enrolled in this category.</response>
    [HttpDelete("api/categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category is null) return NotFoundProblem($"Category {id} was not found.");
        if (category.Event.OrganiserID != CurrentUserId) return ForbiddenProblem("You can only delete categories on your own events.");
        if (await db.Enrolments.AnyAsync(e => e.CategoryID == id))
            return ConflictProblem("This category has enrolments and cannot be deleted.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
