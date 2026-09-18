using RentalApp.Application.Common;

namespace RentalApp.Application.Applications;

public interface IApplicationQueryService
{
    Task<ApplicationDetailDto?> GetAsync(int id, string userId, bool isManager, CancellationToken ct = default);
    Task<PagedResult<ApplicationListItemDto>> ListAsync(ApplicationListQuery query, CancellationToken ct = default);
}
