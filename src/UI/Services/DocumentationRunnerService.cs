using BacapGenerator.Configuration;
using Spectre.Console;
using UI.Styling;
using WbGenerator.DocGen.Documentation.Models;
using WbGenerator.DocGen.Documentation.Services;
using WbGenerator.Exceptions;

namespace UI.Services;

/// <summary>
/// A shared UI service responsible for running WorldBorder documentation export
/// and rendering status reports to the console.
/// </summary>
/// <param name="exportService">The documentation export service.</param>
/// <param name="globalConfig">The global application configuration.</param>
public sealed class DocumentationRunnerService(
    WbDocumentationExportService exportService,
    GlobalConfig globalConfig)
{
    /// <summary>
    /// Executes the documentation export and renders the resulting status directly to the console.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> if documentation generation succeeded or was skipped because it is not configured;
    /// otherwise, <see langword="false"/> if an error occurred.
    /// </returns>
    /// <example>
    /// <code>
    /// if (!await documentationRunner.RunAndRenderReportAsync())
    ///     return;
    /// </code>
    /// </example>
    public async Task<bool> RunAndRenderReportAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            DocumentationExportResult result = null!;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("teal"))
                .StartAsync("Generating documentation JSON...", async _ =>
                {
                    result = await exportService.GenerateExportFileAsync(
                        globalConfig.DocumentGenerator,
                        cancellationToken);
                });

            if (result.WasSkipped)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Skipped:[/] {Markup.Escape(result.ErrorMessage ?? "Documentation generation was skipped.")}");
                return true;
            }

            if (!result.IsSuccess)
            {
                TuiTheme.ShowError(result.ErrorMessage ?? "Documentation generation failed unexpectedly.");
                return false;
            }

            TuiTheme.ShowSuccess(
                $"Exported [bold cyan]{result.ExportedCount}[/] advancement(s) " +
                $"([grey]{result.SkippedExcludedCount} excluded skipped[/]) to:\n" +
                $"[cyan]{Markup.Escape(result.OutputPath!)}[/]");

            return true;
        }
        catch (WbRewardMissingException ex)
        {
            TuiTheme.ShowError(
                $"Missing database configuration!\n" +
                $"Advancement: [yellow]{Markup.Escape(ex.AdvancementId)}[/]\n" +
                $"Reward function: [yellow]{Markup.Escape(ex.RewardPath)}[/]\n" +
                $"Please run 'Add Missing Rewards to DB' before generating documentation.");
            return false;
        }
        catch (Exception ex)
        {
            TuiTheme.ShowError($"Failed to generate documentation: {Markup.Escape(ex.Message)}");
            return false;
        }
    }
}