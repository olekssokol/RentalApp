using RentalApp.Domain.Common;
using RentalApp.Domain.Entities;

namespace RentalApp.Application.Properties;

public class PropertyService : IPropertyService
{
    private readonly IPropertyRepository _properties;

    public PropertyService(IPropertyRepository properties) => _properties = properties;

    public Task<IReadOnlyList<PropertyDto>> GetAllAsync(CancellationToken ct = default) =>
        _properties.ListAsync(ct);

    public Task<PropertyDto?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _properties.GetDtoAsync(id, ct);

    public async Task<Result<int>> CreateAsync(CreatePropertyCommand command, CancellationToken ct = default)
    {
        var entity = new Property
        {
            Name = command.Name.Trim(),
            AddressLine1 = command.AddressLine1.Trim(),
            City = command.City.Trim(),
            State = command.State.Trim(),
            PostalCode = command.PostalCode.Trim()
        };
        await _properties.AddAsync(entity, ct);
        await _properties.SaveChangesAsync(ct);
        return Result.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(UpdatePropertyCommand command, CancellationToken ct = default)
    {
        var entity = await _properties.GetByIdAsync(command.Id, ct);
        if (entity is null)
            return Result.Failure("Property not found.");

        entity.Name = command.Name.Trim();
        entity.AddressLine1 = command.AddressLine1.Trim();
        entity.City = command.City.Trim();
        entity.State = command.State.Trim();
        entity.PostalCode = command.PostalCode.Trim();
        await _properties.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _properties.GetWithUnitsForDeleteAsync(id, ct);
        if (entity is null)
            return Result.Failure("Property not found.");

        if (entity.Units.Any(u => u.Applications.Count > 0 || u.Leases.Count > 0))
            return Result.Failure("Cannot delete a property that has units with applications or leases.");

        _properties.Remove(entity);
        await _properties.SaveChangesAsync(ct);
        return Result.Success();
    }
}
