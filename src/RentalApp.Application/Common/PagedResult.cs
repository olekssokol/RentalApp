namespace RentalApp.Application.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int FilteredTotal, int Page, int PageSize);
