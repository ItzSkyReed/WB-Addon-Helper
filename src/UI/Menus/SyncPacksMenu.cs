using BacapGenerator.Configuration;
using Spectre.Console;
using UI.Configuration;
using UI.Interfaces;
using UI.Styling;

namespace UI.Menus;

/// <summary>
/// Sub-menu for synchronizing the WorldBorder (WB) addon datapack to the local Minecraft world folder.
/// </summary>
/// <param name="config">The user configuration providing synchronization target paths.</param>
/// <param name="globalConfig">The global configuration containing the source path for the WB addon.</param>
public sealed class SyncPacksMenu(UserConfig config, GlobalConfig globalConfig) : IMainMenuAction
{
    private const string TargetFolderName = "wb_addon";

    /// <summary>
    /// Gets the display title of the menu action.
    /// </summary>
    public string Title => "Sync WB Addon to Minecraft";

    /// <summary>
    /// Executes the synchronization workflow by verifying source and destination directories and copying the WB addon datapack.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ExecuteAsync()
    {
        TuiTheme.RenderHeader("Sync WB Addon");

        var sourcePath = globalConfig.WbAddonPath;
        var isSourceValid = !string.IsNullOrWhiteSpace(sourcePath) && Directory.Exists(sourcePath);

        if (!config.IsWorldDatapacksPathValid || !isSourceValid)
        {
            TuiTheme.ShowError("Synchronization cannot proceed due to invalid directory paths.");
            TuiTheme.Space();
            RenderPathStatus("WB Addon Source", sourcePath, isSourceValid);
            RenderPathStatus("WORLD_DATAPACKS_PATH", config.WorldDatapacksPath, config.IsWorldDatapacksPathValid);
            TuiTheme.Space();
            TuiTheme.ShowInfo("Please verify your configuration and ensure both directories exist.");
            TuiTheme.WaitForKey();
            return Task.CompletedTask;
        }

        RenderPathStatus("WB Addon Source", sourcePath, isSourceValid);
        RenderPathStatus("Base World Target", config.WorldDatapacksPath, config.IsWorldDatapacksPathValid);
        TuiTheme.Space();

        SyncDatapack(sourcePath!, config.WorldDatapacksPath!);

        TuiTheme.ShowSuccess("Synchronization finished!");
        TuiTheme.WaitForKey();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Synchronizes the WB addon directory into the destination world datapacks directory.
    /// </summary>
    /// <param name="wbPath">The source path to the WorldBorder addon folder.</param>
    /// <param name="targetWorldDatapacksDir">The absolute destination directory for world datapacks.</param>
    private static void SyncDatapack(string wbPath, string targetWorldDatapacksDir)
    {
        var destinationDirectory = Path.Combine(targetWorldDatapacksDir, TargetFolderName);

        TuiTheme.RunProgress("Syncing WB Addon", [wbPath], source =>
        {
            CopyDirectory(source, destinationDirectory);
        });

        TuiTheme.ShowSuccess($"Synchronized WB Addon to {destinationDirectory}.");
    }

    /// <summary>
    /// Replaces the destination directory entirely by removing any existing directory and performing a clean copy from the source directory.
    /// </summary>
    /// <param name="sourceDirectory">The directory path to copy from.</param>
    /// <param name="destinationDirectory">The destination directory path to completely recreate and populate.</param>
    /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs during directory deletion or file transfer.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller does not have the required permission to access or delete files.</exception>
    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

        // Clean target directory completely
        if (Directory.Exists(destinationDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(destinationDirectory, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(destinationDirectory, recursive: true);
        }

        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationFilePath = Path.Combine(destinationDirectory, relativePath);
            var destinationSubDir = Path.GetDirectoryName(destinationFilePath);

            if (destinationSubDir != null)
                Directory.CreateDirectory(destinationSubDir);

            File.Copy(file, destinationFilePath, overwrite: true);
        }
    }

    /// <summary>
    /// Displays a standardized status line for an environment or configuration path.
    /// </summary>
    /// <param name="label">The human-readable name of the setting.</param>
    /// <param name="path">The resolved directory path.</param>
    /// <param name="isValid">A value indicating whether the directory exists and is usable.</param>
    /// <param name="isOptional">Indicates whether this path is optional under current conditions.</param>
    private static void RenderPathStatus(string label, string? path, bool isValid, bool isOptional = false)
    {
        var escapedLabel = Markup.Escape(label);
        var escapedPath = path is not null ? Markup.Escape(path) : null;

        if (isValid)
        {
            AnsiConsole.MarkupLine($"  [Grey84]{escapedLabel}:[/] [green]{escapedPath}[/]");
            return;
        }

        if (isOptional && string.IsNullOrWhiteSpace(path))
        {
            AnsiConsole.MarkupLine($"  [Grey84]{escapedLabel}:[/] [grey]not configured (optional)[/]");
            return;
        }

        var displayValue = string.IsNullOrWhiteSpace(escapedPath)
            ? "not configured"
            : $"directory not found ({escapedPath})";

        AnsiConsole.MarkupLine($"  [Grey84]{escapedLabel}:[/] [red]{displayValue}[/]");
    }
}