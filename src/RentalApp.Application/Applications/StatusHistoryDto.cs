using RentalApp.Domain.Enums;

namespace RentalApp.Application.Applications;

public record StatusHistoryDto(
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    string ChangedByDisplayName,
    string? Comment,
    DateTime ChangedAtUtc);
