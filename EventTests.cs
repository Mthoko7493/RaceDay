using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

public class EventTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201()
    {
        var (organiser, user) = await factory.SignUpAsync("Organiser");
        var ev = await organiser.CreateEventAsync();

        Assert.Equal(user.UserId, ev.OrganiserId);
        Assert.Equal("Run", ev.EventType);
    }

    [Fact]
    public async Task Anonymous_CannotCreateEvent_Returns401()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/events", new { name = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_InvalidType_Returns400()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Bad Event", description = "", eventDate = DateTime.Now.AddDays(5),
            location = "Cape Town", distanceKm = 5, eventType = "Swim"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_MissingName_Returns400()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "", eventDate = DateTime.Now.AddDays(5), location = "Cape Town", distanceKm = 5, eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anyone_CanListAndViewEvents_WithoutLogin()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var ev = await organiser.CreateEventAsync();
        var anonymous = factory.NewClient();

        var list = await anonymous.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var events = await list.Content.ReadFromJsonAsync<List<EventDto>>();
        Assert.Contains(events!, e => e.EventId == ev.EventId);

        var detail = await anonymous.GetAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task GetEvent_Unknown_Returns404()
    {
        var response = await factory.NewClient().GetAsync("/api/events/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PastEvents_AreNotListedAsUpcoming()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var past = await organiser.CreateEventAsync(daysAhead: -10);

        var events = await factory.NewClient().GetFromJsonAsync<List<EventDto>>("/api/events");
        Assert.DoesNotContain(events!, e => e.EventId == past.EventId);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent_Returns200()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var ev = await organiser.CreateEventAsync();

        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", new
        {
            name = "Soweto 21K", description = "Updated", eventDate = DateTime.Now.AddDays(40),
            location = "Soweto", distanceKm = 21, eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Soweto 21K", (await response.Content.ReadFromJsonAsync<EventDto>())!.Name);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await factory.SignUpAsync("Organiser");
        var (other, _) = await factory.SignUpAsync("Organiser");
        var ev = await owner.CreateEventAsync();

        var response = await other.PutAsJsonAsync($"/api/events/{ev.EventId}", new
        {
            name = "Hijack", eventDate = DateTime.Now.AddDays(40), location = "X", distanceKm = 5, eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdateUnknownEvent_Returns404()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var response = await organiser.PutAsJsonAsync("/api/events/999999", new
        {
            name = "Ghost", eventDate = DateTime.Now.AddDays(40), location = "X", distanceKm = 5, eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_Returns204_ThenGone()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var ev = await organiser.CreateEventAsync();
        await organiser.CreateCategoryAsync(ev.EventId);

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await organiser.GetAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteEventThatHasEnrolments()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        await participant.EnrolAsync(ev.EventId, category.CategoryId);

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await factory.SignUpAsync("Organiser");
        var (other, _) = await factory.SignUpAsync("Organiser");
        var ev = await owner.CreateEventAsync();

        var response = await other.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
