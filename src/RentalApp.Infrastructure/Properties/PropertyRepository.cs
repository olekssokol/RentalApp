using Microsoft.EntityFrameworkCore;
using RentalApp.Application.Properties;
using RentalApp.Domain.Entities;
using RentalApp.Infrastructure.Persistence;

namespace RentalApp.Infrastructure.Properties;

public class PropertyRepository : IPropertyRepository
{
    private readonly AppDbContext _db;

    public PropertyRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PropertyDto>> ListAsync(CancellationToken ct = default) =>
        await _db.Properties
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PropertyDto(p.Id, p.Name, p.AddressLine1, p.City, p.State, p.PostalCode, p.Units.Count))
            .ToListAsync(ct);

    public Task<PropertyDto?> GetDtoAsync(int id, CancellationToken ct = default) =>
        _db.Properties
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PropertyDto(p.Id, p.Name, p.AddressLine1, p.City, p.State, p.PostalCode, p.Units.Count))
            .FirstOrDefaultAsync(ct);

    public Task<Property?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Properties.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Property?> GetWithUnitsForDeleteAsync(int id, CancellationToken ct = default) =>
        _db.Properties
            .Include(p => p.Units)
            .ThenInclude(u => u.Applications)
            .Include(p => p.Units)
            .ThenInclude(u => u.Leases)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Property property, CancellationToken ct = default) =>
        await _db.Properties.AddAsync(property, ct);

    public void Remove(Property property) => _db.Properties.Remove(property);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
