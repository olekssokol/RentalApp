namespace RentalApp.Application.Applications;

public record AddPropertyManagerNoteCommand(
    int ApplicationId,
    string AuthorUserId,
    string AuthorDisplayName,
    string Text,
    bool IsManager);
