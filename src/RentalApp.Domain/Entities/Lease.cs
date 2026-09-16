namespace RentalApp.Domain.Entities;

public class Lease : Entity
{
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
