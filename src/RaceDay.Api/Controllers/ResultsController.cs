using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Tags("Results")]
public class ResultsController(RaceDayDbContext db) : ApiControllerBase
{
    private IQueryable<Result> ResultsWithGraph =>
        db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Event)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category)
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant);

    private async Task<Dictionary<int, int>> FinisherCounts(IEnumerable<int> eventIds)
    {
        var ids = eventIds.Distinct().ToList();
        return await db.Results
            .Where(r => ids.Contains(r.Enrolment.EventID))
            .GroupBy(r => r.Enrolment.EventID)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);
    }

    /// <summary>Capture the finish time and finishing position for an enrolled Participant.</summary>
    /// <remarks>Only the Organiser who owns the event can do this. The enrolment status becomes Confirmed.</remarks>
    /// <response code="201">Result recorded.</response>
    /// <response code="400">Validation failed, or the enrolment was cancelled.</response>
    /// <response code="403">Caller is not an Organiser, or does not own the event.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">A result has already been recorded.</response>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Record(int enrolmentId, [FromBody] ResultRequest request)
    {
        var enrolment = await db.Enrolments.Include(e => e.Event).Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentID == enrolmentId);

        if (enrolment is null) return NotFoundProblem($"Enrolment {enrolmentId} was not found.");
        if (enrolment.Event.OrganiserID != CurrentUserId) return ForbiddenProblem("You can only capture results for your own events.");
        if (enrolment.Result is not null) return ConflictProblem("A result has already been recorded for this enrolment.");
        if (enrolment.Status == EnrolmentStatus.Cancelled) return BadRequestProblem("Cannot record a result for a cancelled enrolment.");
        if (request.FinishTime!.Value <= TimeSpan.Zero) return BadRequestProblem("FinishTime must be greater than zero.");

        var result = new Result
        {
            EnrolmentID = enrolmentId,
            FinishTime = request.FinishTime.Value,
            FinishPosition = request.FinishPosition,
            RecordedAt = DateTime.Now
        };
        enrolment.Status = EnrolmentStatus.Confirmed;
        db.Results.Add(result);
        await db.SaveChangesAsync();

        var saved = await ResultsWithGraph.FirstAsync(r => r.ResultID == result.ResultID);
        var totals = await FinisherCounts(new[] { saved.Enrolment.EventID });
        return StatusCode(StatusCodes.Status201Created, saved.ToResponse(totals[saved.Enrolment.EventID]));
    }

    /// <summary>Return the logged-in Participant's full personal result history.</summary>
    /// <response code="200">Array of results with event name, date, category, finish time and position.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("api/results/my")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var userId = CurrentUserId;
        var results = await ResultsWithGraph
            .Where(r => r.Enrolment.ParticipantID == userId)
            .OrderByDescending(r => r.Enrolment.Event.EventDate)
            .ToListAsync();

        var totals = await FinisherCounts(results.Select(r => r.Enrolment.EventID));
        return Ok(results.Select(r => r.ToResponse(totals[r.Enrolment.EventID])));
    }

    /// <summary>Return all published results for an event (public), ordered by finishing position.</summary>
    /// <response code="200">Array of results.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await db.Events.AnyAsync(e => e.EventID == eventId))
            return NotFoundProblem($"Event {eventId} was not found.");

        var results = await ResultsWithGraph
            .Where(r => r.Enrolment.EventID == eventId)
            .OrderBy(r => r.FinishPosition)
            .ToListAsync();

        return Ok(results.Select(r => r.ToResponse(results.Count)));
    }
}
