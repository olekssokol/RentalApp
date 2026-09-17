using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RentalApp.Web.ViewModels.Units;

public class UnitFormViewModel
{
    public int? Id { get; set; }

    public int PropertyId { get; set; }

    [Required, MaxLength(50), Display(Name = "Unit number")]
    public string UnitNumber { get; set; } = string.Empty;

    [Range(0, 20)]
    public int Bedrooms { get; set; }

    [Range(0.01, 100000), Display(Name = "Monthly rent")]
    public decimal MonthlyRent { get; set; }

    [Required, Display(Name = "Unit type")]
    public int UnitTypeId { get; set; }

    public IEnumerable<SelectListItem> UnitTypes { get; set; } = [];
}
