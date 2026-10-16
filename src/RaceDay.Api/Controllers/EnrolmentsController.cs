using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;
//create
[Tags("Event Enrolments")]
public class EnrolmentsController(RaceDayDbContext db) : ApiControllerBase
{
    private IQueryable<Enrolment> EnrolmentsWithGraph =>
        db.Enrolments.Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant);

    /// <summary>Enrol the logged-in Participant into an event under a chosen category.</summary>
    /// <remarks>New enrolments start with status "Pending". Re-enrolling after a cancellation reactivates the enrolment.</remarks>
    /// <response code="201">Enrolment recorded with status Pending.</response>
    /// <response code="400">Validation failed, or the event has already taken place.</response>
    /// <response code="403">Caller is not a Participant.</response>
    /// <response code="404">Event, or a category belonging to that event, was not found.</response>
    /// <response code="409">Already enrolled in this event.</response>
    [HttpPost("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, [FromBody] EnrolmentRequest request)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventID == eventId);
        if (ev is null) return NotFoundProblem($"Event {eventId} was not found.");

        var category = await db.Categories.FirstOrDefaultAsync(c => c.CategoryID == request.CategoryId && c.EventID == eventId);
        if (category is null) return NotFoundProblem($"Category {request.CategoryId} does not exist for event {eventId}.");

        if (ev.EventDate < DateTime.Now) return BadRequestProblem("This event has already taken place.");

        var userId = CurrentUserId;
        var existing = await db.Enrolments.FirstOrDefaultAsync(e => e.ParticipantID == userId && e.EventID == eventId);

        int enrolmentId;
        if (existing is null)
        {
            var enrolment = new Enrolment
            {
                ParticipantID = userId,
                EventID = eventId,
                CategoryID = category.CategoryID,
                EnrolmentDate = DateTime.Now,
                Status = EnrolmentStatus.Pending
            };
            db.Enrolments.Add(enrolment);
            await db.SaveChangesAsync();
            enrolmentId = enrolment.EnrolmentID;
        }
        else if (existing.Status == EnrolmentStatus.Cancelled)
        {
            existing.CategoryID = category.CategoryID;
            existing.EnrolmentDate = DateTime.Now;
            existing.Status = EnrolmentStatus.Pending;
            await db.SaveChangesAsync();
            enrolmentId = existing.EnrolmentID;
        }
        else
        {
            return ConflictProblem("You are already enrolled in this event.");
        }

        var saved = await EnrolmentsWithGraph.FirstAsync(e => e.EnrolmentID == enrolmentId);
        return StatusCode(StatusCodes.Status201Created, saved.ToResponse());
    }

    /// <summary>List all enrolments of the logged-in Participant.</summary>
    /// <response code="200">Array of enrolments, newest event first.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("api/enrolments/my")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var userId = CurrentUserId;
        var items = await EnrolmentsWithGraph
            .Where(e => e.ParticipantID == userId)
            .OrderByDescending(e => e.Event.EventDate)
            .ToListAsync();
        return Ok(items.Select(e => e.ToResponse()));
    }

    /// <summary>List all enrolments for an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="403">Caller is not an Organiser, or does not own the event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventID == eventId);
        if (ev is null) return NotFoundProblem($"Event {eventId} was not found.");
        if (ev.OrganiserID != CurrentUserId) return ForbiddenProblem("You can only view enrolments for your own events.");

        var items = await EnrolmentsWithGraph.Where(e => e.EventID == eventId).OrderBy(e => e.EnrolmentDate).ToListAsync();
        return Ok(items.Select(e => e.ToResponse()));
    }

    /// <summary>Cancel the logged-in Participant's own enrolment.</summary>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="403">Caller is not a Participant, or does not own this enrolment.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">A result has already been recorded for this enrolment.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await db.Enrolments.Include(e => e.Result).FirstOrDefaultAsync(e => e.EnrolmentID == id);
        if (enrolment is null) return NotFoundProblem($"Enrolment {id} was not found.");
        if (enrolment.ParticipantID != CurrentUserId) return ForbiddenProblem("You can only cancel your own enrolments.");
        if (enrolment.Result is not null) return ConflictProblem("A result has already been recorded; this enrolment cannot be cancelled.");

        enrolment.Status = EnrolmentStatus.Cancelled;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
