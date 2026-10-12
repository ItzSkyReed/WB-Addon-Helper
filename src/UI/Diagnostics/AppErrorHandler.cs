using BacapGenerator.Configuration.Exceptions;
using Core.Registries.Exceptions;
using Microsoft.Extensions.Options;
using Spectre.Console;
using UI.Styling;

namespace UI.Diagnostics;

/// <summary>
/// Central dispatcher responsible for rendering readable TUI alerts and diagnosing fatal runtime exceptions.
/// </summary>
public static class AppErrorHandler
{
    /// <summary>
    /// Handles and renders any application exception with contextual troubleshooting tips.
    /// </summary>
    /// <param name="exception">The encountered exception.</param>
    /// <param name="showStackTrace">Whether to display full stack trace details.</param>
    /// <returns>The suggested exit code (e.g. 1 for failure).</returns>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     await app.RunAsync();
    /// }
    /// catch (Exception ex)
    /// {
    ///     return AppErrorHandler.Handle(ex, verbose);
    /// }
    /// </code>
    /// </example>
    public static int Handle(Exception exception, bool showStackTrace = false)
    {
        switch (exception)
        {
            case RegistryLoadException regEx:
                RegistryErrorHandler.RenderError(regEx);
                break;

            case ConfigurationTemplateException tmplEx:
                ConfigurationErrorHandler.RenderTemplateError(tmplEx);
                break;

            case DatapackConfigurationException dpEx:
                RenderDatapackError(dpEx);
                break;

            case DuplicateDocumentSectionException dupSecEx:
                RenderDuplicateDocumentSectionError(dupSecEx);
                break;

            case OptionsValidationException:
            case InvalidOperationException when exception.Source?.Contains("Configuration") == true:
                ConfigurationErrorHandler.RenderBootstrapError(exception);
                break;


            // Binders throw InvalidOperationException containing FormatException on bad TypeConverter conversion
            case InvalidOperationException { InnerException: FormatException } invEx:
                ConfigurationErrorHandler.RenderBindingError(invEx);
                break;

            case FileNotFoundException fnfEx:
                RenderFileNotFoundError(fnfEx);
                break;

            case DirectoryNotFoundException dnfEx:
                RenderDirectoryNotFoundError(dnfEx);
                break;

            default:
                RenderGenericCrash(exception);
                break;
        }

        if (showStackTrace)
        {
            RenderStackTraceTree(exception);
        }

        return 1;
    }

    /// <summary>
    /// Displays a styled TUI alert when multiple datapacks use the same document generation section name.
    /// </summary>
    /// <param name="ex">The duplicate document section exception.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ex"/> is <see langword="null"/>.</exception>
    private static void RenderDuplicateDocumentSectionError(DuplicateDocumentSectionException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        // Escape string values to prevent Spectre.Console from crashing on accidental brackets in names
        var section = Markup.Escape(ex.SectionName);
        var datapacks = string.Join(", ", ex.DatapackIds.Select(id => $"[cyan]{Markup.Escape(id)}[/]"));

        var content = $"Conflict detected in document generator configuration.\n" +
                      $"Multiple enabled datapacks are claiming the identical section name: [bold red]{section}[/]\n\n" +
                      $"[bold white]Conflicting Datapacks:[/] {datapacks}\n\n" +
                      $"[bold white]Tip:[/] Each active datapack must have a unique [yellow]document_generator.section_name[/] " +
                      $"(e.g., 'default', 'Hardcore') in [white]config.yaml[/] to prevent overwriting each other's documentation.";

        TuiTheme.ShowAlert("[bold red] Configuration Conflict [/]", content, Color.Red);
    }

    /// <summary>
    /// Displays a styled TUI alert for datapack structural configuration errors.
    /// </summary>
    /// <param name="ex">The datapack configuration exception containing error details and kind.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ex"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     datapack.Validate(datapackId, minecraftData);
    /// }
    /// catch (DatapackConfigurationException ex)
    /// {
    ///     RenderDatapackError(ex);
    /// }
    /// </code>
    /// </example>
    private static void RenderDatapackError(DatapackConfigurationException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var description = Markup.Escape(ex.Message);

        var tip = ex.Kind switch
        {
            DatapackErrorKind.MissingPath =>
                "Specify a relative or absolute filesystem directory in [white]config.yaml[/].",

            DatapackErrorKind.MissingNamespace =>
                "Ensure both [yellow]main_namespace[/] and [yellow]reward_namespace[/] are defined without colons (e.g. 'bacaped', 'bacaped_rewards').",

            DatapackErrorKind.MissingParentDatapack =>
                "Check that the parent datapack key exists under the [yellow]datapacks[/] section.",

            DatapackErrorKind.InvalidChecklistConfiguration =>
                "Verify that [yellow]id[/], [yellow]trigger_scoreboard[/], and [yellow]storage_name[/] are specified correctly in [yellow]checklists[/].",

            DatapackErrorKind.MissingLanguagePackPath =>
                "Specify [yellow]language_pack.path[/] in [white]config.yaml[/] pointing to the resource pack root directory.",

            DatapackErrorKind.InvalidLanguagePackConfiguration =>
                "Ensure the path exists, contains valid characters, and [yellow]ignored_keys[/] has no empty entries.",

            DatapackErrorKind.InvalidValidationRuleConfiguration =>
                "Verify rule severity names (info, warning, error) and ensure allowed strings lists have no empty entries.",

            DatapackErrorKind.InvalidDatapackType =>
                "Supported datapack types are: [cyan]reference[/], [cyan]addon[/], [cyan]compatibility_addon[/].",

            DatapackErrorKind.InvalidScoreConfiguration =>
                "Check [yellow]custom_scores[/] and [yellow]custom_points[/] sections in [white]config.yaml[/]. " +
                "Ensure valid [cyan]id[/], [cyan]scoreboard[/], [cyan]file_path[/] (.mcfunction), and filter options.",

            DatapackErrorKind.InvalidCompatibilityAddonSettings or DatapackErrorKind.InvalidValidationSettings =>
                "Supported boolean values are [yellow]true[/] and [yellow]false[/]",

            DatapackErrorKind.InvalidDocumentGeneratorSettings =>
                "Documentation section name is missing. Specify [yellow]document_generator.section_name[/] in [white]config.yaml[/].",

            _ => "Check datapack definitions in [white]config.yaml[/]."
        };

        var content = $"{description}\n\n[bold white]Tip:[/] {tip}";
        TuiTheme.ShowAlert("[bold red] Datapack Settings Error [/]", content, Color.Red);
    }

    /// <summary>
    /// Displays a styled TUI alert for missing filesystem files.
    /// </summary>
    /// <param name="ex">The file not found exception.</param>
    private static void RenderFileNotFoundError(FileNotFoundException ex)
    {
        var path = ex.FileName is not null ? Markup.Escape(ex.FileName) : "Unknown path";
        var content = $"Required file could not be found:\n[bold cyan]{path}[/]\n\n" +
                      $"[bold white]Tip:[/] Check if the file was deleted or moved, and verify relative paths in [yellow]config.yaml[/].";

        TuiTheme.ShowAlert("[bold red] File Not Found [/]", content, Color.Red);
    }

    /// <summary>
    /// Displays a styled TUI alert for missing directories.
    /// </summary>
    /// <param name="ex">The directory not found exception.</param>
    private static void RenderDirectoryNotFoundError(DirectoryNotFoundException ex)
    {
        var content = $"Target directory does not exist:\n[bold cyan]{Markup.Escape(ex.Message)}[/]\n\n" +
                      $"[bold white]Tip:[/] Ensure all referenced folders exist or create them before executing.";

        TuiTheme.ShowAlert("[bold red] Directory Not Found [/]", content, Color.Red);
    }

    /// <summary>
    /// Displays an unexpected runtime error panel.
    /// </summary>
    /// <param name="ex">The unhandled exception.</param>
    private static void RenderGenericCrash(Exception ex)
    {
        var content = $"An unexpected error halted execution:\n\n" +
                      $"[bold white]{Markup.Escape(ex.GetType().Name)}:[/] {Markup.Escape(ex.Message)}\n\n" +
                      $"[Gray84]Report this issue to the developer[/]";

        TuiTheme.ShowAlert("[bold red] Unexpected Fatal Error [/]", content, Color.Red);
    }

    /// <summary>
    /// Visualizes the exception and its inner causes as an interactive Spectre.Console Tree.
    /// </summary>
    /// <param name="exception">The root exception to trace.</param>
    private static void RenderStackTraceTree(Exception exception)
    {
        var tree = new Tree("[bold red]Exception Details[/]");
        var current = exception;

        while (current != null)
        {
            var node = tree.AddNode(
                $"[bold yellow]{Markup.Escape(current.GetType().FullName ?? current.GetType().Name)}[/]: {Markup.Escape(current.Message)}");

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                var stackLines = current.StackTrace.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var stackNode = node.AddNode("[grey]Stack Trace[/]");
                foreach (var line in stackLines)
                {
                    stackNode.AddNode($"[Gray84]{Markup.Escape(line)}[/]");
                }
            }

            current = current.InnerException;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
    }
}