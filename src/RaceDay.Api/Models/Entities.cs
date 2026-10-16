namespace RaceDay.Api.Models;

/// <summary>Allowed role names. Stored in the Roles table.</summary>
public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };

    public static string? Normalize(string? value) =>
        All.FirstOrDefault(r => string.Equals(r, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class EventTypes
{
    public static readonly string[] All = { "Run", "Walk", "Cycle" };

    public static string? Normalize(string? value) =>
        All.FirstOrDefault(r => string.Equals(r, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class EnrolmentStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

public class Role
{
    public int RoleID { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public ICollection<User> Users { get; set; } = new List<User>();
}

public class User
{
    public int UserID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int RoleID { get; set; }
    public Role Role { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Event
{
    public int EventID { get; set; }
    public int OrganiserID { get; set; }
    public User Organiser { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Category
{
    public int CategoryID { get; set; }
    public int EventID { get; set; }
    public Event Event { get; set; } = null!;
    public string CategoryName { get; set; } = string.Empty;
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal? DistanceKm { get; set; }
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Enrolment
{
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public User Participant { get; set; } = null!;
    public int EventID { get; set; }
    public Event Event { get; set; } = null!;
    public int CategoryID { get; set; }
    public Category Category { get; set; } = null!;
    public DateTime EnrolmentDate { get; set; } = DateTime.Now;
    public string Status { get; set; } = EnrolmentStatus.Pending;
    public Result? Result { get; set; }
}

public class Result
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }
    public Enrolment Enrolment { get; set; } = null!;
    public TimeSpan FinishTime { get; set; }
    public int FinishPosition { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.Now;
}
