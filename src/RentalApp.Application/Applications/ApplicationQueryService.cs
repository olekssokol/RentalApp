using RentalApp.Application.Common;
using RentalApp.Domain.Services;

namespace RentalApp.Application.Applications;

public class ApplicationQueryService : IApplicationQueryService
{
    private readonly IRentalApplicationRepository _applications;

    public ApplicationQueryService(IRentalApplicationRepository applications) =>
        _applications = applications;

    public async Task<ApplicationDetailDto?> GetAsync(int id, string userId, bool isManager, CancellationToken ct = default)
    {
        var application = await _applications.GetDetailAsync(id, ct);
        if (application is null)
            return null;
        if (!isManager && !ApplicationMembership.IsMember(application, userId))
            return null;

        return ApplicationMapper.ToDetail(application);
    }

    public Task<PagedResult<ApplicationListItemDto>> ListAsync(ApplicationListQuery query, CancellationToken ct = default) =>
        _applications.ListAsync(query, ct);
}
