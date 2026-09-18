using RentalApp.Domain.Enums;

namespace RentalApp.Application.Applications;

public record ApplicationListQuery(
    string UserId,
    bool IsManager,
    ApplicationStatus? Status,
    int? PropertyId,
    int Page = 1,
    int PageSize = 10,
    ApplicationSortField Sort = ApplicationSortField.Updated,
    ApplicationSortDirection Direction = ApplicationSortDirection.Descending)
{
    public int NormalizedPage => Math.Max(1, Page);
    public int NormalizedPageSize => PageSize is 10 or 20 or 50 ? PageSize : 10;
    public ApplicationSortField NormalizedSort => Enum.IsDefined(Sort) ? Sort : ApplicationSortField.Updated;
    public ApplicationSortDirection NormalizedDirection =>
        Enum.IsDefined(Direction) ? Direction : ApplicationSortDirection.Descending;
}
