using RentalApp.Domain.Enums;

namespace RentalApp.Web.ViewComponents;

public record ApplicationHeaderModel(string PropertyName, string UnitNumber, ApplicationStatus Status);
