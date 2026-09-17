namespace RentalApp.Web.ViewModels.Units;

public class DeleteUnitFormViewModel
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
}
