using RentalApp.Domain.Common;
using RentalApp.Domain.Entities;

namespace RentalApp.Application.Applications;

public class PropertyManagerNoteService : IPropertyManagerNoteService
{
    public const int MaxTextLength = 2000;
    private readonly IPropertyManagerNoteRepository _notes;

    public PropertyManagerNoteService(IPropertyManagerNoteRepository notes) => _notes = notes;

    public async Task<Result<IReadOnlyList<PropertyManagerNoteDto>>> ListAsync(
        int applicationId, bool isManager, CancellationToken ct = default)
    {
        if (!isManager)
            return Result.Failure<IReadOnlyList<PropertyManagerNoteDto>>("Property manager access is required.");
        if (!await _notes.ApplicationExistsAsync(applicationId, ct))
            return Result.Failure<IReadOnlyList<PropertyManagerNoteDto>>("Application not found.");

        return Result.Success(await _notes.ListAsync(applicationId, ct));
    }

    public async Task<Result<PropertyManagerNoteDto>> GetAsync(
        int applicationId, int noteId, bool isManager, CancellationToken ct = default)
    {
        if (!isManager)
            return Result.Failure<PropertyManagerNoteDto>("Property manager access is required.");
        var note = await _notes.GetAsync(applicationId, noteId, ct);
        return note is null
            ? Result.Failure<PropertyManagerNoteDto>("Note not found.")
            : Result.Success(Map(note));
    }

    public async Task<Result<int>> AddAsync(AddPropertyManagerNoteCommand command, CancellationToken ct = default)
    {
        if (!command.IsManager)
            return Result.Failure<int>("Property manager access is required.");
        var text = command.Text.Trim();
        if (text.Length == 0 || text.Length > MaxTextLength)
            return Result.Failure<int>($"Note text is required and cannot exceed {MaxTextLength} characters.");
        if (!await _notes.ApplicationExistsAsync(command.ApplicationId, ct))
            return Result.Failure<int>("Application not found.");

        var now = DateTime.UtcNow;
        var note = new PropertyManagerNote
        {
            RentalApplicationId = command.ApplicationId,
            AuthorUserId = command.AuthorUserId,
            AuthorDisplayName = command.AuthorDisplayName,
            Text = text,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await _notes.AddAsync(note, ct);
        await _notes.SaveChangesAsync(ct);
        return Result.Success(note.Id);
    }

    public async Task<Result> UpdateAsync(UpdatePropertyManagerNoteCommand command, CancellationToken ct = default)
    {
        if (!command.IsManager)
            return Result.Failure("Property manager access is required.");
        var text = command.Text.Trim();
        if (text.Length == 0 || text.Length > MaxTextLength)
            return Result.Failure($"Note text is required and cannot exceed {MaxTextLength} characters.");

        var note = await _notes.GetAsync(command.ApplicationId, command.NoteId, ct);
        if (note is null)
            return Result.Failure("Note not found.");

        note.Text = text;
        note.UpdatedAtUtc = DateTime.UtcNow;
        await _notes.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static PropertyManagerNoteDto Map(PropertyManagerNote note) =>
        new(note.Id, note.RentalApplicationId, note.AuthorDisplayName, note.Text, note.CreatedAtUtc, note.UpdatedAtUtc);
}
