using System.ComponentModel.DataAnnotations;

namespace RentalApp.Web.ViewModels.Applications;

public class PropertyManagerNoteFormViewModel
{
    public int ApplicationId { get; set; }
    public int? Id { get; set; }

    [Required, MaxLength(2000), Display(Name = "Note")]
    public string Text { get; set; } = string.Empty;
}
