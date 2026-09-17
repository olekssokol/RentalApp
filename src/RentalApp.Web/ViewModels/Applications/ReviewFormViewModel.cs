using System.ComponentModel.DataAnnotations;
using RentalApp.Domain.Enums;

namespace RentalApp.Web.ViewModels.Applications;

public class ReviewFormViewModel
{
    public int ApplicationId { get; set; }

    [Required]
    public ReviewOutcome Outcome { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }

    public string ApplicantName { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
}
