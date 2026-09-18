namespace RentalApp.Domain.Entities;

public class PropertyManagerNote : Entity
{
    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
