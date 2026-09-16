using System.Security.Claims;
using RentalApp.Infrastructure.Identity;

namespace RentalApp.Web.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User id claim is missing.");

    public static bool IsManager(this ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.PropertyManager);

    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.Identity?.Name ?? "User";
}
