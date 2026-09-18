using RentalApp.Application.Applications;
using RentalApp.Domain.Entities;

namespace RentalApp.UnitTests.Support;

internal sealed class PropertyManagerNoteRepositoryFake : IPropertyManagerNoteRepository
{
    public bool ApplicationExists { get; set; } = true;
    public List<PropertyManagerNote> Notes { get; } = [];
    public int ReadCount { get; private set; }
    public int SaveCount { get; private set; }

    public Task<bool> ApplicationExistsAsync(int applicationId, CancellationToken ct = default)
    {
        ReadCount++;
        return Task.FromResult(ApplicationExists);
    }

    public Task<IReadOnlyList<PropertyManagerNoteDto>> ListAsync(int applicationId, CancellationToken ct = default)
    {
        ReadCount++;
        IReadOnlyList<PropertyManagerNoteDto> result = Notes
            .Where(n => n.RentalApplicationId == applicationId)
            .Select(n => new PropertyManagerNoteDto(
                n.Id, n.RentalApplicationId, n.AuthorDisplayName, n.Text, n.CreatedAtUtc, n.UpdatedAtUtc))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<PropertyManagerNote?> GetAsync(int applicationId, int noteId, CancellationToken ct = default)
    {
        ReadCount++;
        return Task.FromResult(Notes.SingleOrDefault(n =>
            n.RentalApplicationId == applicationId && n.Id == noteId));
    }

    public Task AddAsync(PropertyManagerNote note, CancellationToken ct = default)
    {
        note.Id = Notes.Count + 1;
        Notes.Add(note);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
