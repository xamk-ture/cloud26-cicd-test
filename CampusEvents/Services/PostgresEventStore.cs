using CampusEvents.Data;
using CampusEvents.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusEvents.Services;

public class PostgresEventStore(AppDbContext db) : IEventStore
{
    public IReadOnlyList<CampusEvent> GetAll() =>
        db.Events
            .AsNoTracking()
            .Include(e => e.Registrations)
            .OrderBy(e => e.StartsAtUtc)
            .ToList();

    public CampusEvent? GetById(Guid id) =>
        db.Events
            .AsNoTracking()
            .Include(e => e.Registrations)
            .FirstOrDefault(e => e.Id == id);

    public CampusEvent Create(CreateEventRequest request)
    {
        CampusEvent campusEvent = new()
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Location = request.Location.Trim(),
            StartsAtUtc = DateTime.SpecifyKind(request.StartsAtUtc, DateTimeKind.Utc),
            Capacity = request.Capacity,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Events.Add(campusEvent);
        db.SaveChanges();
        return campusEvent;
    }

    public CampusEvent? Update(Guid id, UpdateEventRequest request)
    {
        CampusEvent? existing = db.Events
            .Include(e => e.Registrations)
            .FirstOrDefault(e => e.Id == id);

        if (existing is null)
        {
            return null;
        }

        if (request.Capacity < existing.SeatsTaken)
        {
            throw new InvalidOperationException(
                $"Capacity cannot be less than current registrations ({existing.SeatsTaken}).");
        }

        existing.Title = request.Title.Trim();
        existing.Description = request.Description.Trim();
        existing.Location = request.Location.Trim();
        existing.StartsAtUtc = DateTime.SpecifyKind(request.StartsAtUtc, DateTimeKind.Utc);
        existing.Capacity = request.Capacity;

        db.SaveChanges();
        return existing;
    }

    public bool Delete(Guid id)
    {
        CampusEvent? existing = db.Events.FirstOrDefault(e => e.Id == id);
        if (existing is null)
        {
            return false;
        }

        db.Events.Remove(existing);
        db.SaveChanges();
        return true;
    }

    public (RegistrationResponse? Registration, string? Error) Register(Guid eventId, RegisterRequest request)
    {
        CampusEvent? campusEvent = db.Events
            .Include(e => e.Registrations)
            .FirstOrDefault(e => e.Id == eventId);

        if (campusEvent is null)
        {
            return (null, "Event not found.");
        }

        if (campusEvent.IsFull)
        {
            return (null, "Event is full.");
        }

        string email = request.AttendeeEmail.Trim().ToLowerInvariant();
        if (campusEvent.Registrations.Any(r => r.AttendeeEmail.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            return (null, "This email is already registered for the event.");
        }

        Registration registration = new()
        {
            Id = Guid.NewGuid(),
            AttendeeName = request.AttendeeName.Trim(),
            AttendeeEmail = email,
            RegisteredAtUtc = DateTime.UtcNow
        };

        campusEvent.Registrations.Add(registration);
        db.Add(registration);
        db.SaveChanges();

        RegistrationResponse response = new(
            registration.Id,
            campusEvent.Id,
            registration.AttendeeName,
            registration.AttendeeEmail,
            registration.RegisteredAtUtc);

        return (response, null);
    }
}
