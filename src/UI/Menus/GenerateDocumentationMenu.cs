using Spectre.Console;
using UI.Interfaces;
using UI.Services;
using UI.Styling;

namespace UI.Menus;

/// <summary>
/// Terminal UI action that generates the WorldBorder Addon JSON documentation file.
/// </summary>
/// <param name="documentationRunner">The shared documentation runner service.</param>
public sealed class GenerateDocumentationMenu(DocumentationRunnerService documentationRunner) : IMainMenuAction
{
    public string Title => "Generate Documentation";

    /// <summary>
    /// Executes the documentation export workflow and displays the console report.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteAsync()
    {
        TuiTheme.RenderHeader(Title);
        AnsiConsole.MarkupLine("[grey]Running documentation generator...[/]");

        await documentationRunner.RunAndRenderReportAsync();

        TuiTheme.WaitForKey();
    }
}