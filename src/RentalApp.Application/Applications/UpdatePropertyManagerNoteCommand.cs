namespace RentalApp.Application.Applications;

public record UpdatePropertyManagerNoteCommand(int ApplicationId, int NoteId, string Text, bool IsManager);
