using RentalApp.Domain.Enums;

namespace RentalApp.Application.Applications;

public record ApplicationListQuery(
    string UserId,
    bool IsManager,
    ApplicationStatus? Status,
    int? PropertyId);
