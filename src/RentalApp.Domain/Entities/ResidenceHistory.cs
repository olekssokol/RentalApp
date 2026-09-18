namespace RentalApp.Domain.Entities;

public class ResidenceHistory : Entity
{
    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public string? Address { get; set; }
    public string? LandlordName { get; set; }
    public string? LandlordPhone { get; set; }
    public DateOnly? MoveInDate { get; set; }
    public DateOnly? MoveOutDate { get; set; }
}
