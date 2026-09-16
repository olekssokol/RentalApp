using RentalApp.Domain.Enums;

namespace RentalApp.Domain.Entities;

public class ApplicationStatusHistory : Entity
{
    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public ApplicationStatus? FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }

    public string ChangedByUserId { get; set; } = string.Empty;
    public string ChangedByDisplayName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
