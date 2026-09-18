using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentalApp.Application.Applications;
using RentalApp.Application.Properties;
using RentalApp.Application.Units;
using RentalApp.Infrastructure.Applications;
using RentalApp.Infrastructure.Identity;
using RentalApp.Infrastructure.Persistence;
using RentalApp.Infrastructure.Properties;
using RentalApp.Infrastructure.Units;

namespace RentalApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        services.AddScoped<IPropertyRepository, PropertyRepository>();
        services.AddScoped<IUnitRepository, UnitRepository>();
        services.AddScoped<IRentalApplicationRepository, RentalApplicationRepository>();
        services.AddScoped<IPropertyManagerNoteRepository, PropertyManagerNoteRepository>();

        return services;
    }
}
