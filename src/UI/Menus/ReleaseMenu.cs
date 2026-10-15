using System.Text.RegularExpressions;
using Spectre.Console;
using UI.Interfaces;
using UI.Styling;
using WbGenerator.Exceptions;
using WbGenerator.Services.Interfaces;

namespace UI.Menus;

/// <summary>
/// Terminal UI action that coordinates WorldBorder addon releases, prompting for semantic versioning
/// and compiling distribution archives.
/// </summary>
/// <param name="releaseService">The service executing the release workflow.</param>
public partial class ReleaseMenu(IWbReleaseService releaseService) : IMainMenuAction
{
    [GeneratedRegex(@"^[0-9]+\.[0-9]+(\.[0-9]+)?(-(alpha|beta))?$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPatternRegex();

    public string Title => "Make WB Addon Release";

    /// <summary>
    /// Prompts the user for a version string and executes the release packaging pipeline.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteAsync()
    {
        TuiTheme.RenderHeader("WorldBorder Addon Release");

        var version = AskValidVersion();
        if (version is null)
        {
            TuiTheme.ShowWarning("Release operation cancelled.");
            TuiTheme.WaitForKey();
            return;
        }

        try
        {
            var zipPath = string.Empty;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("teal"))
                .StartAsync("Generating addon files and compiling release archive...", async _ =>
                {
                    zipPath = await releaseService.CreateReleaseAsync(version);
                });

            Console.WriteLine();
            TuiTheme.ShowSuccess($"Release archive successfully created at:\n[cyan]{Markup.Escape(zipPath)}[/]");
        }
        catch (WbRewardMissingException ex)
        {
            Console.WriteLine();
            TuiTheme.ShowError(
                $"Missing database configuration!\n" +
                $"Advancement: [yellow]{Markup.Escape(ex.AdvancementId)}[/]\n" +
                $"Reward function: [yellow]{Markup.Escape(ex.RewardPath)}[/]\n" +
                $"Please run 'Add Missing' before creating a release.");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            TuiTheme.ShowError($"Failed to compile release: {Markup.Escape(ex.Message)}");
        }

        TuiTheme.WaitForKey();
    }

    /// <summary>
    /// Prompts the user for a valid semantic-style version or cancellation.
    /// </summary>
    /// <returns>The validated version string, or <see langword="null"/> if aborted.</returns>
    private static string? AskValidVersion()
    {
        var input = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]datapack version[/] (or 'cancel'):")
                .PromptStyle("teal")
                .Validate(val =>
                {
                    var trimmed = val.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        return ValidationResult.Error("Version cannot be empty.");

                    if (trimmed.Equals("cancel", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Equals("q", StringComparison.OrdinalIgnoreCase))
                    {
                        return ValidationResult.Success();
                    }

                    if (!VersionPatternRegex().IsMatch(trimmed))
                    {
                        return ValidationResult.Error(
                            "Invalid format! Expected: X.Y, X.Y.Z, X.Y-alpha, X.Y.Z-beta etc.");
                    }

                    return ValidationResult.Success();
                })).Trim();

        return input.Equals("cancel", StringComparison.OrdinalIgnoreCase) ||
               input.Equals("q", StringComparison.OrdinalIgnoreCase)
            ? null
            : input;
    }
}