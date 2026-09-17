using RentalApp.Application.Properties;
using RentalApp.Application.Units;

namespace RentalApp.Web.ViewModels.Properties;

public class PropertyDetailsViewModel
{
    public required PropertyDto Property { get; init; }
    public required IReadOnlyList<UnitDto> Units { get; init; }
}
