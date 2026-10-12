using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BacapGenerator.Configuration;
using Microsoft.Extensions.Logging;
using WbGenerator.Persistence;
using WbGenerator.Services;
using WbGenerator.Services.Interfaces;

namespace UI.Startup;

public static class WbServiceCollectionExtensions
{
    /// <summary>
    /// Registers the WorldBorder database context and associated reward services.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The configured service collection for chaining.</returns>
    public static IServiceCollection AddWbAddonServices(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var globalConfig = serviceProvider.GetRequiredService<GlobalConfig>();
            var dbPath = Path.IsPathRooted(globalConfig.DatabasePath)
                ? globalConfig.DatabasePath
                : Path.Combine(AppContext.BaseDirectory, globalConfig.DatabasePath);

            var dbDirectory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            options.UseSqlite($"Data Source={dbPath}")
                .UseLoggerFactory(LoggerFactory.Create(builder =>
                {
                    builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
                }));
        });

        services.AddScoped<IWbRewardService, WbRewardService>();
        services.AddScoped<IWbAddonGeneratorService, WbAddonGeneratorService>();
        services.AddScoped<IWbReleaseService, WbReleaseService>();
        services.AddScoped<IWbRewardFunctionGenerator, WbRewardFunctionGenerator>();

        return services;
    }
}