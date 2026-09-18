using RentalApp.Domain.Entities;

namespace RentalApp.Application.Applications;

public interface IPropertyManagerNoteRepository
{
    Task<bool> ApplicationExistsAsync(int applicationId, CancellationToken ct = default);
    Task<IReadOnlyList<PropertyManagerNoteDto>> ListAsync(int applicationId, CancellationToken ct = default);
    Task<PropertyManagerNote?> GetAsync(int applicationId, int noteId, CancellationToken ct = default);
    Task AddAsync(PropertyManagerNote note, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
