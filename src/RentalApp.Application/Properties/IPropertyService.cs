using RentalApp.Domain.Common;

namespace RentalApp.Application.Properties;

public interface IPropertyService
{
    Task<IReadOnlyList<PropertyDto>> GetAllAsync(CancellationToken ct = default);
    Task<PropertyDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(CreatePropertyCommand command, CancellationToken ct = default);
    Task<Result> UpdateAsync(UpdatePropertyCommand command, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
