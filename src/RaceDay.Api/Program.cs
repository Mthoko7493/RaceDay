using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

var builder = WebApplication.CreateBuilder(args);
//create a new web application builder and configure services for the RaceDay API, including database context, session management, controllers, and Swagger documentation.
// ASPNETCORE_ENVIRONMENT=Testing swaps SQL Server for an isolated in-memory database (used by the unit tests).
var isTesting = builder.Environment.IsEnvironment("Testing");
var inMemoryName = $"RaceDayTests-{Guid.NewGuid()}";

builder.Services.AddDbContext<RaceDayDbContext>(options =>
{
    if (isTesting)
        options.UseInMemoryDatabase(inMemoryName);
    else
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Server-side session: the session stores the user id and role, the browser only holds the session cookie.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "RaceDay API", Version = "v1", Description = "Event management API for South African road running, walking and cycling events. Log in via POST /api/auth/login first - the session cookie is then sent automatically by Swagger UI." });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Create the schema if the database is empty (does nothing if the Part 1 SQL script already created it),
// then make sure the two roles exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    db.Database.EnsureCreated();
    foreach (var roleName in RoleNames.All)
    {
        if (!db.Roles.Any(r => r.RoleName == roleName))
            db.Roles.Add(new Role { RoleName = roleName });
    }
    db.SaveChanges();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    options.RoutePrefix = "swagger";
});

app.UseSession();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

// Needed so the test project can use WebApplicationFactory<Program>.
public partial class Program { }
