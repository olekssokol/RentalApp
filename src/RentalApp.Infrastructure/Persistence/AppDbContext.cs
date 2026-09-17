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
    public DbSet<RentalApplication> RentalApplications => Set<RentalApplication>();
    public DbSet<ResidenceHistory> ResidenceHistories => Set<ResidenceHistory>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<Lease> Leases => Set<Lease>();

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

        builder.Entity<RentalApplication>(e =>
        {
            e.Property(x => x.ApplicantUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.CurrentAddress).HasMaxLength(500);
            e.HasOne(x => x.Unit).WithMany(u => u.Applications).HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ApplicantUserId);
        });

        builder.Entity<ResidenceHistory>(e =>
        {
            e.Property(x => x.Address).HasMaxLength(500).IsRequired();
            e.Property(x => x.LandlordName).HasMaxLength(200).IsRequired();
            e.Property(x => x.LandlordPhone).HasMaxLength(50).IsRequired();
            e.HasOne(x => x.RentalApplication).WithMany(a => a.Residences).HasForeignKey(x => x.RentalApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApplicationStatusHistory>(e =>
        {
            e.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.ChangedByDisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.HasOne(x => x.RentalApplication).WithMany(a => a.StatusHistory).HasForeignKey(x => x.RentalApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Lease>(e =>
        {
            e.HasOne(x => x.Unit).WithMany(u => u.Leases).HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RentalApplication).WithOne(a => a.Lease).HasForeignKey<Lease>(x => x.RentalApplicationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.RentalApplicationId).IsUnique();
            e.HasIndex(x => x.UnitId);
        });
    }
}
