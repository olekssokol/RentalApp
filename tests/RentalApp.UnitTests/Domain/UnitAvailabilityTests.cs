using RentalApp.Domain.Entities;
using RentalApp.Domain.Services;

namespace RentalApp.UnitTests.Domain;

public class UnitAvailabilityTests
{
    [Theory]
    [InlineData("2026-09-20", false)]
    [InlineData("2026-09-21", true)]
    [InlineData("2027-01-01", true)]
    [InlineData("2027-09-20", true)]
    [InlineData("2027-09-21", false)]
    public void HasActiveLease_DateRelativeToTerm_IncludesBothBoundaries(string asOf, bool active)
    {
        var lease = new Lease { StartDate = new(2026, 9, 21), EndDate = new(2027, 9, 20) };
        var today = DateOnly.ParseExact(asOf, "yyyy-MM-dd");

        Assert.Equal(active, UnitAvailability.HasActiveLease([lease], today));
        Assert.Equal(!active, UnitAvailability.IsAvailable([lease], today));
    }

    [Fact]
    public void IsAvailable_WithoutLeases_ReturnsTrue()
    {
        Assert.False(UnitAvailability.HasActiveLease([], new DateOnly(2026, 9, 21)));
        Assert.True(UnitAvailability.IsAvailable([], new DateOnly(2026, 9, 21)));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IsAvailable_HistoricalAndFutureLeases_OnlyCurrentLeaseBlocks(bool includeCurrent, bool available)
    {
        var today = new DateOnly(2026, 9, 21);
        List<Lease> leases = [
            new() { StartDate = today.AddYears(-2), EndDate = today.AddDays(-1) },
            new() { StartDate = today.AddDays(1), EndDate = today.AddYears(1) }
        ];
        if (includeCurrent)
            leases.Add(new Lease { StartDate = today.AddMonths(-1), EndDate = today.AddMonths(1) });

        Assert.Equal(available, UnitAvailability.IsAvailable(leases, today));
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(true, 20, true)]
    [InlineData(false, null, false)]
    [InlineData(false, 10, true)]
    [InlineData(false, 20, false)]
    public void CanAssignUnitType_ActiveOrRetainedInactive_EnforcesAssignmentRule(bool active, int? currentTypeId, bool allowed)
    {
        var selectedType = new UnitType { Id = 10, IsActive = active };

        Assert.Equal(allowed, UnitAvailability.CanAssignUnitType(selectedType, currentTypeId));
    }
}
