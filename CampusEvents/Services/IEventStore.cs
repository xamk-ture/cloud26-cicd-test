using CampusEvents.Models;

namespace CampusEvents.Services;

public interface IEventStore
{
    IReadOnlyList<CampusEvent> GetAll();
    CampusEvent? GetById(Guid id);
    CampusEvent Create(CreateEventRequest request);
    CampusEvent? Update(Guid id, UpdateEventRequest request);
    bool Delete(Guid id);
    (RegistrationResponse? Registration, string? Error) Register(Guid eventId, RegisterRequest request);
}
