namespace RentalApp.Domain.Entities;

public class UnitType : Entity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}
