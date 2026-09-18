using RentalApp.Application.Applications;

namespace RentalApp.Web.ViewModels.Applications;

public class PropertyManagerNotesViewModel
{
    public int ApplicationId { get; init; }
    public IReadOnlyList<PropertyManagerNoteDto> Items { get; init; } = [];
}
