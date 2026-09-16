using RentalApp.Domain.Entities;

namespace RentalApp.Application.Properties;

public interface IPropertyRepository
{
    Task<IReadOnlyList<PropertyDto>> ListAsync(CancellationToken ct = default);
    Task<PropertyDto?> GetDtoAsync(int id, CancellationToken ct = default);
    Task<Property?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Property?> GetWithUnitsForDeleteAsync(int id, CancellationToken ct = default);
    Task AddAsync(Property property, CancellationToken ct = default);
    void Remove(Property property);
    Task SaveChangesAsync(CancellationToken ct = default);
}
