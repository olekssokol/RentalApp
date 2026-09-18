namespace RentalApp.Application.Applications;

public record PropertyManagerNoteDto(
    int Id,
    int ApplicationId,
    string AuthorDisplayName,
    string Text,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
