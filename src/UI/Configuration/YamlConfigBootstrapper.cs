using System.Reflection;
using Spectre.Console;
using UI.Styling;

namespace UI.Configuration;

public static class YamlConfigBootstrapper
{
    /// <summary>
    /// Ensures that the configuration file exists on disk.
    /// If missing, it extracts the default configuration from embedded resources,
    /// saves it to disk, displays a formatted TUI warning, and terminates execution.
    /// </summary>
    /// <param name="targetFilePath">The destination path where the configuration file should reside.</param>
    /// <param name="resourceName">The fully qualified embedded resource name (e.g., "ProjectNamespace.config.yaml").</param>
    /// <param name="exitApplication">
    /// If set to <see langword="true"/>, cleanly halts the process with code 1 after notifying the user;
    /// otherwise, throws a <see cref="FileNotFoundException"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when the embedded resource is missing from the assembly.</exception>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="exitApplication"/> is false and the config file was newly generated.</exception>
    /// <example>
    /// <code>
    /// YamlConfigBootstrapper.EnsureConfigExists("config.yaml", "BacapGenerator.default_wb_config.yaml");
    /// </code>
    /// </example>
    public static void EnsureConfigExists(string targetFilePath, string resourceName, bool exitApplication = true)
    {
        if (File.Exists(targetFilePath))
            return;

        var assembly = Assembly.GetExecutingAssembly();

        using var resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream == null)
            throw new InvalidOperationException(
                $"Cannot find embedded resource '{resourceName}'. " +
                $"Make sure it is marked as <EmbeddedResource> in the .csproj file.");

        // Create directory structure if the path contains nested folders
        var directory = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using (FileStream fileStream = new(targetFilePath, FileMode.Create, FileAccess.Write))
        {
            resourceStream.CopyTo(fileStream);
        }

        var fullPath = Path.GetFullPath(targetFilePath);
        var escapedPath = Markup.Escape(fullPath);

        // Render visual feedback
        TuiTheme.ShowAlert(
            header: "[bold yellow] Configuration Required [/]",
            content: $"[yellow]The configuration file was not found.[/]\n\n" +
                     $"A default template has been generated at:\n" +
                     $"[bold cyan]{escapedPath}[/]\n\n" +
                     $"[white]Please edit the file with your desired settings and restart the application.[/]",
            borderColor: Color.Yellow);

        if (!exitApplication)
            throw new FileNotFoundException(
                $"Configuration template created at '{fullPath}'. Please configure it before restarting.",
                targetFilePath);

        TuiTheme.WaitForKey();
        Environment.Exit(1);

        throw new FileNotFoundException(
            $"Configuration template created at '{fullPath}'. Please configure it before restarting.",
            targetFilePath);
    }
}