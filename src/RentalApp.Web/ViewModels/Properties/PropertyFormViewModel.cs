using System.ComponentModel.DataAnnotations;

namespace RentalApp.Web.ViewModels.Properties;

public class PropertyFormViewModel
{
    public int? Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(300), Display(Name = "Address")]
    public string AddressLine1 { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string State { get; set; } = string.Empty;

    [Required, MaxLength(20), Display(Name = "Postal code")]
    public string PostalCode { get; set; } = string.Empty;
}
