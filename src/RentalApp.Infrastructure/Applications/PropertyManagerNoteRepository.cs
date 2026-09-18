using Microsoft.EntityFrameworkCore;
using RentalApp.Application.Applications;
using RentalApp.Domain.Entities;
using RentalApp.Infrastructure.Persistence;

namespace RentalApp.Infrastructure.Applications;

public class PropertyManagerNoteRepository : IPropertyManagerNoteRepository
{
    private readonly AppDbContext _db;

    public PropertyManagerNoteRepository(AppDbContext db) => _db = db;

    public Task<bool> ApplicationExistsAsync(int applicationId, CancellationToken ct = default) =>
        _db.RentalApplications.AnyAsync(a => a.Id == applicationId, ct);

    public async Task<IReadOnlyList<PropertyManagerNoteDto>> ListAsync(int applicationId, CancellationToken ct = default) =>
        await _db.PropertyManagerNotes
            .AsNoTracking()
            .Where(n => n.RentalApplicationId == applicationId)
            .OrderByDescending(n => n.UpdatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Select(n => new PropertyManagerNoteDto(
                n.Id, n.RentalApplicationId, n.AuthorDisplayName, n.Text, n.CreatedAtUtc, n.UpdatedAtUtc))
            .ToListAsync(ct);

    public Task<PropertyManagerNote?> GetAsync(int applicationId, int noteId, CancellationToken ct = default) =>
        _db.PropertyManagerNotes.FirstOrDefaultAsync(
            n => n.Id == noteId && n.RentalApplicationId == applicationId, ct);

    public Task AddAsync(PropertyManagerNote note, CancellationToken ct = default) =>
        _db.PropertyManagerNotes.AddAsync(note, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
