using System.ComponentModel.DataAnnotations;

namespace RentalApp.Web.ViewModels.Applications;

public class ResidenceFormViewModel
{
    public int ApplicationId { get; set; }
    public int? Id { get; set; }

    [Required, MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [Required, MaxLength(200), Display(Name = "Landlord name")]
    public string LandlordName { get; set; } = string.Empty;

    [Required, MaxLength(50), Display(Name = "Landlord phone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [Required, Display(Name = "Move-in date")]
    [DataType(DataType.Date)]
    public DateOnly? MoveInDate { get; set; }

    [Display(Name = "Move-out date")]
    [DataType(DataType.Date)]
    public DateOnly? MoveOutDate { get; set; }
}
