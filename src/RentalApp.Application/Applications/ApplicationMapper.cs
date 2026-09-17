using RentalApp.Domain.Entities;
using RentalApp.Domain.Services;

namespace RentalApp.Application.Applications;

internal static class ApplicationMapper
{
    public static ApplicationDetailDto ToDetail(RentalApplication application) =>
        new(
            application.Id,
            application.Status,
            application.CurrentSection,
            application.ApplicantUserId,
            application.UnitId,
            application.Unit.Property.Name,
            application.Unit.UnitNumber,
            application.FullName,
            application.Phone,
            application.Email,
            application.CurrentAddress,
            application.ApplicantInfoSaved,
            application.ResidenceHistorySaved,
            ApplicationRules.CanEditSections(application.Status),
            application.Residences
                .OrderBy(r => r.MoveInDate)
                .Select(r => new ResidenceDto(r.Id, r.Address, r.LandlordName, r.LandlordPhone, r.MoveInDate, r.MoveOutDate))
                .ToList(),
            application.StatusHistory
                .OrderByDescending(h => h.ChangedAtUtc)
                .Select(h => new StatusHistoryDto(h.FromStatus, h.ToStatus, h.ChangedByDisplayName, h.Comment, h.ChangedAtUtc))
                .ToList());
}
