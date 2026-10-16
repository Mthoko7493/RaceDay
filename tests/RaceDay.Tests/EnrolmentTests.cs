using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;
// Tests for enrolment and category management, including participant enrolment, organiser viewing of enrolments, and category CRUD operations.
public class EnrolmentTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Participant_CanEnrol_StatusIsPending_AndIsRecorded()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, user) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var enrolment = await participant.EnrolAsync(ev.EventId, category.CategoryId);

        Assert.Equal("Pending", enrolment.Status);
        Assert.Equal(user.UserId, enrolment.ParticipantId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/my");
        Assert.Contains(mine!, e => e.EnrolmentId == enrolment.EnrolmentId && e.CategoryId == category.CategoryId);
    }

    [Fact]
    public async Task Participant_CannotEnrolTwiceInSameEvent_Returns409()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        await participant.EnrolAsync(ev.EventId, category.CategoryId);

        var second = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownEvent_Returns404()
    {
        var (participant, _) = await factory.SignUpAsync("Participant");
        var response = await participant.PostAsJsonAsync("/api/events/999999/enrolments", new { categoryId = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_CategoryFromAnotherEvent_Returns404()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var eventA = await organiser.CreateEventAsync();
        var eventB = await organiser.CreateEventAsync();
        var categoryOfB = await organiser.CreateCategoryAsync(eventB.EventId);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventA.EventId}/enrolments", new { categoryId = categoryOfB.CategoryId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithoutCategoryId_Returns400()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_PastEvent_Returns400()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync(daysAhead: -5);
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForOwnEvent()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(ev.EventId, category.CategoryId);

        var list = await organiser.GetFromJsonAsync<List<EnrolmentDto>>($"/api/events/{ev.EventId}/enrolments");

        Assert.Contains(list!, e => e.EnrolmentId == enrolment.EnrolmentId);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await factory.SignUpAsync("Organiser");
        var (other, _) = await factory.SignUpAsync("Organiser");
        var ev = await owner.CreateEventAsync();

        var response = await other.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_OnlySeesOwnEnrolments()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (alice, _) = await factory.SignUpAsync("Participant");
        var (bob, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var aliceEnrolment = await alice.EnrolAsync(ev.EventId, category.CategoryId);

        var bobsList = await bob.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/my");

        Assert.DoesNotContain(bobsList!, e => e.EnrolmentId == aliceEnrolment.EnrolmentId);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_AndReEnrol()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(ev.EventId, category.CategoryId);

        var cancel = await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/my");
        Assert.Equal("Cancelled", mine!.Single(e => e.EnrolmentId == enrolment.EnrolmentId).Status);

        var again = await participant.EnrolAsync(ev.EventId, category.CategoryId);
        Assert.Equal("Pending", again.Status);
    }

    [Fact]
    public async Task Participant_CannotCancelSomeoneElsesEnrolment_Returns403()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (alice, _) = await factory.SignUpAsync("Participant");
        var (bob, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await alice.EnrolAsync(ev.EventId, category.CategoryId);

        var response = await bob.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_UnknownEnrolment_Returns404()
    {
        var (participant, _) = await factory.SignUpAsync("Participant");
        var response = await participant.DeleteAsync("/api/enrolments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Categories_AreListedPublicly_AndOwnerCanEditAndDelete()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var list = await factory.NewClient().GetFromJsonAsync<List<CategoryDto>>($"/api/events/{ev.EventId}/categories");
        Assert.Contains(list!, c => c.CategoryId == category.CategoryId);

        var put = await organiser.PutAsJsonAsync($"/api/categories/{category.CategoryId}", new { categoryName = "Under 20", minAge = 0, maxAge = 19, distanceKm = 5 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var delete = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Category_MinAgeAboveMaxAge_Returns400()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var ev = await organiser.CreateEventAsync();

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { categoryName = "Bad", minAge = 50, maxAge = 20 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Category_WithEnrolments_CannotBeDeleted_Returns409()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        await participant.EnrolAsync(ev.EventId, category.CategoryId);

        var response = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Categories_ForUnknownEvent_Returns404()
    {
        var response = await factory.NewClient().GetAsync("/api/events/999999/categories");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
