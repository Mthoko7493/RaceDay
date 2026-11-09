using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;
// Tests that the API enforces role-based access control correctly.
/// <summary>Participants cannot use Organiser features, Organisers cannot use Participant features, anonymous users get 401.</summary>
public class RoleEnforcementTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var (participant, _) = await factory.SignUpAsync("Participant");
        var response = await participant.PostAsJsonAsync("/api/events", new
        {
            name = "Nope", eventDate = DateTime.Now.AddDays(5), location = "X", distanceKm = 5, eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvent_Returns403()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();

        var put = await participant.PutAsJsonAsync($"/api/events/{ev.EventId}", new
        {
            name = "Nope", eventDate = DateTime.Now.AddDays(5), location = "X", distanceKm = 5, eventType = "Run"
        });
        var delete = await participant.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotManageCategories_Returns403()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var post = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { categoryName = "Hack" });
        var put = await participant.PutAsJsonAsync($"/api/categories/{category.CategoryId}", new { categoryName = "Hack" });
        var delete = await participant.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolmentList_Returns403()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();

        var response = await participant.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotRecordResult_Returns403()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var (participant, _) = await factory.SignUpAsync("Participant");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(ev.EventId, category.CategoryId);

        var response = await participant.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "01:00:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUseParticipantOnlyEndpoints_Returns403()
    {
        var (organiser, _) = await factory.SignUpAsync("Organiser");

        var myEnrolments = await organiser.GetAsync("/api/enrolments/my");
        var myResults = await organiser.GetAsync("/api/results/my");
        var cancel = await organiser.DeleteAsync("/api/enrolments/1");

        Assert.Equal(HttpStatusCode.Forbidden, myEnrolments.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, myResults.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/profile")]
    [InlineData("GET", "/api/enrolments/my")]
    [InlineData("GET", "/api/results/my")]
    [InlineData("GET", "/api/events/1/enrolments")]
    [InlineData("POST", "/api/events/1/enrolments")]
    [InlineData("POST", "/api/events/1/categories")]
    [InlineData("POST", "/api/enrolments/1/result")]
    [InlineData("DELETE", "/api/events/1")]
    [InlineData("DELETE", "/api/enrolments/1")]
    [InlineData("DELETE", "/api/categories/1")]
    public async Task ProtectedEndpoints_WithoutLogin_Return401(string method, string url)
    {
        var client = factory.NewClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
