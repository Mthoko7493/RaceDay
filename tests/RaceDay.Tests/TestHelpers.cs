using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RaceDay.Tests;

/// <summary>Boots the real API in the "Testing" environment (isolated in-memory database per factory).</summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}

// Subsets of the API responses (extra JSON fields are ignored).
public record UserDto(int UserId, string FullName, string Email, string Role, string? PhoneNumber);
public record CategoryDto(int CategoryId, int EventId, string CategoryName);
public record EventDto(int EventId, int OrganiserId, string Name, string EventType, List<CategoryDto> Categories);
public record EnrolmentDto(int EnrolmentId, int EventId, int CategoryId, int ParticipantId, string Status);
public record ResultDto(int ResultId, int EnrolmentId, int EventId, string FinishTime, int FinishPosition, int TotalFinishers);

public static class TestHelpers
{
    public const string Password = "Passw0rd!";

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@raceday.test";

    /// <summary>A client that keeps cookies, so the server-side session persists between calls.</summary>
    public static HttpClient NewClient(this RaceDayFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    public static object RegisterBody(string email, string role, string password = Password) =>
        new { fullName = "Test User", email, password, role, phoneNumber = "0821234567" };

    /// <summary>Registers a fresh user with the given role and logs them in.</summary>
    public static async Task<(HttpClient Client, UserDto User)> SignUpAsync(this RaceDayFactory factory, string role)
    {
        var client = factory.NewClient();
        var email = UniqueEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email, role));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var user = await login.Content.ReadFromJsonAsync<UserDto>();
        return (client, user!);
    }

    public static async Task<EventDto> CreateEventAsync(this HttpClient organiser, int daysAhead = 30)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Soweto 10K",
            description = "Community fun run",
            eventDate = DateTime.Now.AddDays(daysAhead),
            location = "Soweto, Johannesburg",
            distanceKm = 10,
            eventType = "Run"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EventDto>())!;
    }

    public static async Task<CategoryDto> CreateCategoryAsync(this HttpClient organiser, int eventId, string name = "Senior")
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = name, minAge = 18, maxAge = 99, distanceKm = 10 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    public static async Task<EnrolmentDto> EnrolAsync(this HttpClient participant, int eventId, int categoryId)
    {
        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EnrolmentDto>())!;
    }
}
