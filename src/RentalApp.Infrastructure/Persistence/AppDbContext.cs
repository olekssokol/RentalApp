using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentalApp.Domain.Entities;
using RentalApp.Infrastructure.Identity;

namespace RentalApp.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Property>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.AddressLine1).HasMaxLength(300).IsRequired();
            e.Property(x => x.City).HasMaxLength(100).IsRequired();
            e.Property(x => x.State).HasMaxLength(50).IsRequired();
            e.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
        });

        builder.Entity<UnitType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<Unit>(e =>
        {
            e.Property(x => x.UnitNumber).HasMaxLength(50).IsRequired();
            e.Property(x => x.MonthlyRent).HasPrecision(18, 2);
            e.HasOne(x => x.Property).WithMany(p => p.Units).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.UnitType).WithMany(t => t.Units).HasForeignKey(x => x.UnitTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PropertyId, x.UnitNumber }).IsUnique();
        });
    }
}
