namespace CampusEvents.Models;

public class CampusEvent
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public int Capacity { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Organizer { get; set; }   // nullable: existing rows have no value yet
    public List<Registration> Registrations { get; set; } = [];

    public int SeatsTaken => Registrations.Count;
    public int SeatsLeft => Math.Max(0, Capacity - SeatsTaken);
    public bool IsFull => SeatsTaken >= Capacity;
}

public class Registration
{
    public Guid Id { get; set; }
    public string AttendeeName { get; set; } = string.Empty;
    public string AttendeeEmail { get; set; } = string.Empty;
    public DateTime RegisteredAtUtc { get; set; }
}
