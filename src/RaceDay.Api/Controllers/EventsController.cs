using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;
//create a controller for events with endpoints for listing, getting, creating, updating, and deleting events
[Route("api/events")]
[Tags("Events")]
public class EventsController(RaceDayDbContext db) : ApiControllerBase
{
    private IQueryable<Event> EventsWithGraph =>
        db.Events.Include(e => e.Organiser).Include(e => e.Categories);

    /// <summary>List all upcoming events (public).</summary>
    /// <response code="200">Array of upcoming events, soonest first.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcoming()
    {
        var now = DateTime.Now;
        var events = await EventsWithGraph.Where(e => e.EventDate >= now).OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToResponse()));
    }

    /// <summary>Get the full detail of a single event, including its categories (public).</summary>
    /// <response code="200">Event returned.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await EventsWithGraph.FirstOrDefaultAsync(e => e.EventID == id);
        return ev is null ? NotFoundProblem($"Event {id} was not found.") : Ok(ev.ToResponse());
    }

    /// <summary>Create a new event owned by the logged-in Organiser.</summary>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed (e.g. eventType must be Run, Walk or Cycle).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpPost]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] EventRequest request)
    {
        var type = EventTypes.Normalize(request.EventType);
        if (type is null)
        {
            ModelState.AddModelError(nameof(request.EventType), "EventType must be Run, Walk or Cycle.");
            return ValidationProblem(ModelState);
        }

        var ev = new Event
        {
            OrganiserID = CurrentUserId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = type,
            CreatedAt = DateTime.Now
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var created = await EventsWithGraph.FirstAsync(e => e.EventID == ev.EventID);
        return CreatedAtAction(nameof(GetById), new { id = ev.EventID }, created.ToResponse());
    }

    /// <summary>Update an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Event updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Caller is not an Organiser, or does not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] EventRequest request)
    {
        var ev = await EventsWithGraph.FirstOrDefaultAsync(e => e.EventID == id);
        if (ev is null) return NotFoundProblem($"Event {id} was not found.");
        if (ev.OrganiserID != CurrentUserId) return ForbiddenProblem("You can only modify events you created.");

        var type = EventTypes.Normalize(request.EventType);
        if (type is null)
        {
            ModelState.AddModelError(nameof(request.EventType), "EventType must be Run, Walk or Cycle.");
            return ValidationProblem(ModelState);
        }

        ev.Name = request.Name.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = type;
        await db.SaveChangesAsync();

        return Ok(ev.ToResponse());
    }

    /// <summary>Delete an event owned by the logged-in Organiser (also removes its categories, enrolments and results).</summary>
    /// <response code="204">Event deleted.</response>
    /// <response code="403">Caller is not an Organiser, or does not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await db.Events
            .Include(e => e.Categories)
            .Include(e => e.Enrolments).ThenInclude(en => en.Result)
            .FirstOrDefaultAsync(e => e.EventID == id);

        if (ev is null) return NotFoundProblem($"Event {id} was not found.");
        if (ev.OrganiserID != CurrentUserId) return ForbiddenProblem("You can only delete events you created.");

        // Enrolments reference the event with NO ACTION in the schema, so remove them (and their results) first.
        db.Results.RemoveRange(ev.Enrolments.Where(en => en.Result != null).Select(en => en.Result!));
        db.Enrolments.RemoveRange(ev.Enrolments);
        db.Categories.RemoveRange(ev.Categories);
        db.Events.Remove(ev);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
