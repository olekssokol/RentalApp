namespace RentalApp.Application.Applications;

public interface IApplicationQueryService
{
    Task<ApplicationDetailDto?> GetAsync(int id, string userId, bool isManager, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationListItemDto>> ListAsync(ApplicationListQuery query, CancellationToken ct = default);
}
