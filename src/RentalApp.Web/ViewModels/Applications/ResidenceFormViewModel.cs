using System.ComponentModel.DataAnnotations;

namespace RentalApp.Web.ViewModels.Applications;

public class ResidenceFormViewModel
{
    public int ApplicationId { get; set; }
    public int? Id { get; set; }

    public string? Address { get; set; }

    [Display(Name = "Landlord name")]
    public string? LandlordName { get; set; }

    [Display(Name = "Landlord phone")]
    public string? LandlordPhone { get; set; }

    [Display(Name = "Move-in date")]
    [DataType(DataType.Date)]
    public DateOnly? MoveInDate { get; set; }

    [Display(Name = "Move-out date")]
    [DataType(DataType.Date)]
    public DateOnly? MoveOutDate { get; set; }

    public int ExpectedResidenceHistoryVersion { get; set; }
}
