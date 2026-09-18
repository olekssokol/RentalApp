namespace RentalApp.Web.ViewModels.Applications;

public class DeleteResidenceFormViewModel
{
    public int ApplicationId { get; set; }
    public int Id { get; set; }
    public string? Address { get; set; }
    public int ExpectedResidenceHistoryVersion { get; set; }
}
