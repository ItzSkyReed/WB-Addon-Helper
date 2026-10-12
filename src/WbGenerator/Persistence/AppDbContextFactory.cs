using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace WbGenerator.Persistence;

/// <summary>
/// Design-time factory used by EF Core tooling (e.g., migrations) to instantiate <see cref="AppDbContext"/>.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Creates a new instance of <see cref="AppDbContext"/> configured for SQLite.
    /// </summary>
    /// <param name="args">Arguments provided by the design-time tools.</param>
    /// <returns>A configured <see cref="AppDbContext"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the 'DefaultConnection' connection string is missing or empty in configuration.
    /// </exception>
    /// <example>
    /// CLI execution:
    /// <code>
    /// dotnet ef migrations add InitialCreate
    /// </code>
    /// </example>
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not found.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}