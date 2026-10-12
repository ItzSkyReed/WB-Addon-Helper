namespace WbGenerator.Services.Interfaces;

/// <summary>
/// Provides datapack content generation for the WorldBorder addon.
/// </summary>
public interface IWbAddonGeneratorService
{
    /// <summary>
    /// Generates function tags, initialization functions, and individual reward functions into the target datapack directory.
    /// </summary>
    /// <param name="targetDatapackPath">The destination root path where files will be written.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous generation process.</returns>
    Task GenerateAsync(string targetDatapackPath, CancellationToken cancellationToken = default);
}