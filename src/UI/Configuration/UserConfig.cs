namespace UI.Configuration;

/// <summary>
/// Represents user-specific environment settings and the target synchronization directory.
/// </summary>
public sealed record UserConfig
{
    /// <summary>
    /// Gets the destination directory path for synchronizing world datapacks.
    /// </summary>
    public string? WorldDatapacksPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the world datapack synchronization directory is configured and exists on disk.
    /// </summary>
    public bool IsWorldDatapacksPathValid =>
        !string.IsNullOrWhiteSpace(WorldDatapacksPath) &&
        Directory.Exists(WorldDatapacksPath);
}