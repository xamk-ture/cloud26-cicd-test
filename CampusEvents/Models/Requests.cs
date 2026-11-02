using System.ComponentModel.DataAnnotations;

namespace CampusEvents.Models;

public record CreateEventRequest(
    [property: Required, MinLength(3), MaxLength(120)] string Title,
    [property: Required, MinLength(10), MaxLength(1000)] string Description,
    [property: Required, MinLength(2), MaxLength(120)] string Location,
    [property: Required] DateTime StartsAtUtc,
    [property: Range(1, 500)] int Capacity);

public record UpdateEventRequest(
    [property: Required, MinLength(3), MaxLength(120)] string Title,
    [property: Required, MinLength(10), MaxLength(1000)] string Description,
    [property: Required, MinLength(2), MaxLength(120)] string Location,
    [property: Required] DateTime StartsAtUtc,
    [property: Range(1, 500)] int Capacity);

public record RegisterRequest(
    [property: Required, MinLength(2), MaxLength(100)] string AttendeeName,
    [property: Required, EmailAddress, MaxLength(200)] string AttendeeEmail);

public record EventResponse(
    Guid Id,
    string Title,
    string Description,
    string Location,
    DateTime StartsAtUtc,
    int Capacity,
    int SeatsTaken,
    int SeatsLeft,
    bool IsFull,
    DateTime CreatedAtUtc);

public record RegistrationResponse(
    Guid Id,
    Guid EventId,
    string AttendeeName,
    string AttendeeEmail,
    DateTime RegisteredAtUtc);

public static class EventMappings
{
    public static EventResponse ToResponse(this CampusEvent e) =>
        new(
            e.Id,
            e.Title,
            e.Description,
            e.Location,
            e.StartsAtUtc,
            e.Capacity,
            e.SeatsTaken,
            e.SeatsLeft,
            e.IsFull,
            e.CreatedAtUtc);
}
