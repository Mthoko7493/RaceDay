using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

public class ResultTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    private async Task<(HttpClient Organiser, HttpClient Participant, EventDto Event, EnrolmentDto Enrolment)> SetUpEnrolmentAsync()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(ev.EventId, category.CategoryId);
        return (organiser, participant, ev, enrolment);
    }

    [Fact]
    public async Task Organiser_CanRecordResult_Returns201()
    {
        var (organiser, _, _, enrolment) = await SetUpEnrolmentAsync();

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "01:45:30", finishPosition = 47 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ResultDto>();
        Assert.Equal(47, result!.FinishPosition);
        Assert.Equal("01:45:30", result.FinishTime);
        Assert.Equal(1, result.TotalFinishers);
    }

    [Fact]
    public async Task RecordingResult_ConfirmsTheEnrolment()
    {
        var (organiser, participant, _, enrolment) = await SetUpEnrolmentAsync();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", new { finishTime = "00:50:00", finishPosition = 3 });

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/my");

        Assert.Equal("Confirmed", mine!.Single(e => e.EnrolmentId == enrolment.EnrolmentId).Status);
    }

    [Fact]
    public async Task RecordResult_Twice_Returns409()
    {
        var (organiser, _, _, enrolment) = await SetUpEnrolmentAsync();
        var body = new { finishTime = "01:00:00", finishPosition = 1 };
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", body);

        var second = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task RecordResult_ForAnotherOrganisersEvent_Returns403()
    {
        var (_, _, _, enrolment) = await SetUpEnrolmentAsync();
        var (otherOrganiser, _) = await factory.SignUpAsync("Organiser");

        var response = await otherOrganiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "01:00:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_UnknownEnrolment_Returns404()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var response = await organiser.PostAsJsonAsync("/api/enrolments/999999/result", new { finishTime = "01:00:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_InvalidPosition_Returns400()
    {
        var (organiser, _, _, enrolment) = await SetUpEnrolmentAsync();
        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "01:00:00", finishPosition = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_ForCancelledEnrolment_Returns400()
    {
        var (organiser, participant, _, enrolment) = await SetUpEnrolmentAsync();
        await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "01:00:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_SeesOwnResults_AndCannotSeeOthers()
    {
        var (organiser, participant, ev, enrolment) = await SetUpEnrolmentAsync();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", new { finishTime = "00:55:10", finishPosition = 12 });
        var (stranger, _) = await factory.SignUpAsync("Participant");

        var mine = await participant.GetFromJsonAsync<List<ResultDto>>("/api/results/my");
        var strangers = await stranger.GetFromJsonAsync<List<ResultDto>>("/api/results/my");

        Assert.Contains(mine!, r => r.EventId == ev.EventId && r.FinishPosition == 12);
        Assert.Empty(strangers!);
    }

    [Fact]
    public async Task EventResults_ArePublic_AndOrderedByPosition()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (first, _) = await factory.SignUpAsync("Participant");
        var (second, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolmentFirst = await first.EnrolAsync(ev.EventId, category.CategoryId);
        var enrolmentSecond = await second.EnrolAsync(ev.EventId, category.CategoryId);

        // Record out of order on purpose.
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentSecond.EnrolmentId}/result", new { finishTime = "01:10:00", finishPosition = 2 });
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentFirst.EnrolmentId}/result", new { finishTime = "01:05:00", finishPosition = 1 });

        var results = await factory.NewClient().GetFromJsonAsync<List<ResultDto>>($"/api/events/{ev.EventId}/results");

        Assert.Equal(new[] { 1, 2 }, results!.Select(r => r.FinishPosition).ToArray());
        Assert.All(results!, r => Assert.Equal(2, r.TotalFinishers));
    }

    [Fact]
    public async Task EventResults_UnknownEvent_Returns404()
    {
        var response = await factory.NewClient().GetAsync("/api/events/999999/results");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
