namespace WbGenerator.Services.Interfaces;

/// <summary>
/// Provides release packaging operations for the WorldBorder addon.
/// </summary>
public interface IWbReleaseService
{
    /// <summary>
    /// Assembles a release archive for the specified version.
    /// </summary>
    /// <param name="version">The semantic version string.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The full path to the created release .zip file.</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation or reward generation fails.</exception>
    Task<string> CreateReleaseAsync(string version, CancellationToken cancellationToken = default);
}