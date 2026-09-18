namespace RentalApp.Domain.Entities;

/// <summary>
/// Authoritative application membership. <see cref="RentalApplication.ApplicantUserId"/> is creator metadata only.
/// </summary>
public class ApplicationApplicant
{
    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
