using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;
//create a test class for authentication endpoints, using xunit and the RaceDayFactory fixture
public class AuthenticationTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Register_ValidUser_Returns201_WithoutPassword(string role)
    {
        var client = factory.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(TestHelpers.UniqueEmail(), role));
        //
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(role, body);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = factory.NewClient();
        var email = TestHelpers.UniqueEmail();

        await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(email, "Participant"));
        var second = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(email, "Participant"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var client = factory.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(TestHelpers.UniqueEmail(), "Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var client = factory.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(TestHelpers.UniqueEmail(), "Participant", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var client = factory.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody("not-an-email", "Participant"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var client = factory.NewClient();
        var email = TestHelpers.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(email, "Participant"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = db.Users.Single(u => u.Email == email).PasswordHash;

        Assert.NotEqual(TestHelpers.Password, stored);
        Assert.DoesNotContain(TestHelpers.Password, stored);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200_WithUserIdAndRole()
    {
        var (_, user) = await factory.SignUpAsync("Organiser");

        Assert.True(user.UserId > 0);
        Assert.Equal("Organiser", user.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = factory.NewClient();
        var email = TestHelpers.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(email, "Participant"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var client = factory.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = TestHelpers.UniqueEmail(), password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutLogin_Returns401()
    {
        var client = factory.NewClient();
        var response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_AfterLogin_ReturnsOwnProfile_AndCanBeUpdated()
    {
        var (client, user) = await factory.SignUpAsync("Participant");

        var get = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(user.Email, (await get.Content.ReadFromJsonAsync<UserDto>())!.Email);

        var put = await client.PutAsJsonAsync("/api/profile", new { fullName = "Thabo Mokoena", phoneNumber = "0831112222" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Thabo Mokoena", (await put.Content.ReadFromJsonAsync<UserDto>())!.FullName);
    }

    [Fact]
    public async Task Profile_UpdateWithEmptyName_Returns400()
    {
        var (client, _) = await factory.SignUpAsync("Participant");
        var response = await client.PutAsJsonAsync("/api/profile", new { fullName = "", phoneNumber = "0831112222" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutSession_Returns401()
    {
        var client = factory.NewClient();
        var response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession_SoProtectedEndpointsReturn401()
    {
        var (client, _) = await factory.SignUpAsync("Participant");

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var profile = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }
}
