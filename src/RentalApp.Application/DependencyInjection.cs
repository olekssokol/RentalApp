using Microsoft.Extensions.DependencyInjection;
using RentalApp.Application.Applications;
using RentalApp.Application.Properties;
using RentalApp.Application.Units;

namespace RentalApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<IApplicationQueryService, ApplicationQueryService>();
        services.AddScoped<IApplicationCommandService, ApplicationCommandService>();
        services.AddScoped<IPropertyManagerNoteService, PropertyManagerNoteService>();
        return services;
    }
}
