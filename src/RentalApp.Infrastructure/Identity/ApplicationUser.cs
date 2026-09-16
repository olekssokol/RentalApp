using Microsoft.AspNetCore.Identity;

namespace RentalApp.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
