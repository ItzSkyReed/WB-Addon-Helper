using BacapGenerator.Configuration;
using UI.Configuration;
using UI.Interfaces;
using UI.Styling;

namespace UI.Menus;

/// <summary>
/// Sub-menu for synchronizing loaded datapacks and language resource packs to local Minecraft installation folders.
/// </summary>
/// <param name="registry">The registry containing loaded datapack instances.</param>
/// <param name="config">The user configuration providing synchronization target paths.</param>
public sealed class SyncPacksMenu(UserConfig config, GlobalConfig globalConfig) : IMainMenuAction
{
    public string Title => "Sync Packs to Minecraft";

    /// <summary>
    /// Executes the synchronization workflow by verifying destinations, copying base and compatibility datapacks, and copying resource packs.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ExecuteAsync()
    {
        TuiTheme.RenderHeader("Sync WB Addon");

        SyncDatapack(globalConfig.WbAddonPath, config.WorldDatapacksPath!);

        TuiTheme.ShowSuccess("Synchronization finished!");
        TuiTheme.WaitForKey();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Synchronizes valid source datapack directories into a destination world datapacks directory.
    /// </summary>
    /// <param name="wbPath">The path to the worldborder addon folder.</param>
    /// <param name="targetWorldDatapacksDir">The absolute destination directory for datapacks.</param>
    private static void SyncDatapack(string wbPath, string targetWorldDatapacksDir)
    {


        var destinationDirectory = Path.Combine(targetWorldDatapacksDir, "wb_addon");
        CopyDirectory(wbPath, destinationDirectory);

        TuiTheme.ShowSuccess($"Synchronized WB Addon datapack to {targetWorldDatapacksDir}.");
    }

    /// <summary>
    /// Replaces the destination directory entirely by removing any existing directory and performing a clean copy from the source directory.
    /// </summary>
    /// <param name="sourceDirectory">The directory path to copy from.</param>
    /// <param name="destinationDirectory">The destination directory path to completely recreate and populate.</param>
    /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs during directory deletion or file transfer.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when caller does not have the required permission to access or delete files.</exception>
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
}