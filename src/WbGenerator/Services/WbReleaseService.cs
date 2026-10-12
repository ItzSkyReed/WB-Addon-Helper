using System.IO.Compression;
using BacapGenerator.Configuration;
using WbGenerator.Services.Interfaces;

namespace WbGenerator.Services;

/// <summary>
/// Coordinates the staging, version injection, generation, and archiving of WorldBorder addon releases.
/// </summary>
/// <param name="config">The global configuration containing addon preset and release paths.</param>
/// <param name="generatorService">The generator service synthesizing datapack files.</param>
public sealed class WbReleaseService(
    GlobalConfig config,
    IWbAddonGeneratorService generatorService) : IWbReleaseService
{
    /// <inheritdoc />
    public async Task<string> CreateReleaseAsync(string version, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        config.Validate();

        var releaseName = $"WB-Addon-{version}";
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "WbGenerator", Guid.NewGuid().ToString("N"), releaseName);
        var releaseOutputDir = Path.GetFullPath(config.ReleasePath);
        var targetZipPath = Path.Combine(releaseOutputDir, $"{releaseName}.zip");

        Directory.CreateDirectory(releaseOutputDir);

        try
        {
            // 1. Copy source preset folder to clean staging directory
            CopyDirectory(config.WbAddonPath, stagingDirectory);

            // 2. Replace [<version>] placeholder in version.mcfunction
            await PatchVersionFunctionAsync(stagingDirectory, version, cancellationToken);

            // 3. Generate all init commands, fanpack tags, and rewards
            await generatorService.GenerateAsync(stagingDirectory, cancellationToken);

            // 4. Create ZIP archive
            if (File.Exists(targetZipPath))
                File.Delete(targetZipPath);

            ZipFile.CreateFromDirectory(
                stagingDirectory,
                targetZipPath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);

            return targetZipPath;
        }
        finally
        {
            // Safe cleanup of temporary staging files
            if (Directory.Exists(stagingDirectory))
            {
                try
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
                catch
                {
                    // Ignore transient file lock cleanup issues in OS temp folder
                }
            }
        }
    }

    /// <summary>
    /// Replaces the version placeholder inside 'version.mcfunction' with the release version.
    /// </summary>
    private static async Task PatchVersionFunctionAsync(
        string stagingDir,
        string version,
        CancellationToken cancellationToken)
    {
        var versionFunctionPath = Path.Combine(
            stagingDir, "data", "bacap_wb_addon", "function", "config", "version.mcfunction");

        if (!File.Exists(versionFunctionPath))
            return;

        var content = await File.ReadAllTextAsync(versionFunctionPath, cancellationToken);
        var patchedContent = content
            .Replace("[<version>]", version, StringComparison.OrdinalIgnoreCase)
            .Replace("<version>", version, StringComparison.OrdinalIgnoreCase);

        await File.WriteAllTextAsync(versionFunctionPath, patchedContent, cancellationToken);
    }

    /// <summary>
    /// Copies all files and subdirectories recursively.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source preset directory not found: '{sourceDir}'.");

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destFilePath = Path.Combine(destinationDir, relativePath);
            var destSubDir = Path.GetDirectoryName(destFilePath);

            if (destSubDir is not null && !Directory.Exists(destSubDir))
                Directory.CreateDirectory(destSubDir);

            File.Copy(file, destFilePath, overwrite: true);
        }
    }
}