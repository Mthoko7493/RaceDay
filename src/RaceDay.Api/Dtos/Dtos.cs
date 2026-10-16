using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------- Requests ----------

public class RegisterRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    /// <summary>Organiser or Participant.</summary>
    [Required] public string Role { get; set; } = string.Empty;
    [StringLength(20)] public string? PhoneNumber { get; set; }
}

public class LoginRequest
{
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [StringLength(20)] public string? PhoneNumber { get; set; }
}

public class EventRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required] public DateTime? EventDate { get; set; }
    [Required, StringLength(150)] public string Location { get; set; } = string.Empty;
    [Range(0.01, 9999.99)] public decimal DistanceKm { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public string EventType { get; set; } = string.Empty;
}

public class CategoryRequest
{
    [Required, StringLength(100)] public string CategoryName { get; set; } = string.Empty;
    [Range(0, 120)] public int? MinAge { get; set; }
    [Range(0, 120)] public int? MaxAge { get; set; }
    [Range(0.01, 9999.99)] public decimal? DistanceKm { get; set; }
}

public class EnrolmentRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid categoryId is required.")]
    public int CategoryId { get; set; }
}

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, e.g. 01:45:30.</summary>
    [Required] public TimeSpan? FinishTime { get; set; }
    [Range(1, int.MaxValue)] public int FinishPosition { get; set; }
}

// ---------- Responses ----------

public record UserResponse(int UserId, string FullName, string Email, string Role, string? PhoneNumber, DateTime CreatedAt);

public record CategoryResponse(int CategoryId, int EventId, string CategoryName, int? MinAge, int? MaxAge, decimal? DistanceKm);

public record EventResponse(
    int EventId, int OrganiserId, string OrganiserName, string Name, string? Description,
    DateTime EventDate, string Location, decimal DistanceKm, string EventType,
    IReadOnlyList<CategoryResponse> Categories);

public record EnrolmentResponse(
    int EnrolmentId, int EventId, string EventName, DateTime EventDate, int CategoryId, string CategoryName,
    int ParticipantId, string ParticipantName, DateTime EnrolmentDate, string Status);

public record ResultResponse(
    int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate, string CategoryName,
    int ParticipantId, string ParticipantName, TimeSpan FinishTime, int FinishPosition, int TotalFinishers,
    DateTime RecordedAt);

public static class Mappers
{
    public static UserResponse ToResponse(this User u) =>
        new(u.UserID, u.FullName, u.Email, u.Role.RoleName, u.PhoneNumber, u.CreatedAt);

    public static CategoryResponse ToResponse(this Category c) =>
        new(c.CategoryID, c.EventID, c.CategoryName, c.MinAge, c.MaxAge, c.DistanceKm);

    public static EventResponse ToResponse(this Event e) =>
        new(e.EventID, e.OrganiserID, e.Organiser.FullName, e.Name, e.Description, e.EventDate, e.Location,
            e.DistanceKm, e.EventType, e.Categories.OrderBy(c => c.CategoryID).Select(c => c.ToResponse()).ToList());

    public static EnrolmentResponse ToResponse(this Enrolment x) =>
        new(x.EnrolmentID, x.EventID, x.Event.Name, x.Event.EventDate, x.CategoryID, x.Category.CategoryName,
            x.ParticipantID, x.Participant.FullName, x.EnrolmentDate, x.Status);

    public static ResultResponse ToResponse(this Result r, int totalFinishers) =>
        new(r.ResultID, r.EnrolmentID, r.Enrolment.EventID, r.Enrolment.Event.Name, r.Enrolment.Event.EventDate,
            r.Enrolment.Category.CategoryName, r.Enrolment.ParticipantID, r.Enrolment.Participant.FullName,
            r.FinishTime, r.FinishPosition, totalFinishers, r.RecordedAt);
}
