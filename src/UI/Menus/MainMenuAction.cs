using UI.Actions.Common;
using UI.Extensions;
using UI.Interfaces;
using UI.Styling;

namespace UI.Menus;

/// <summary>
/// The root menu of the application.
/// </summary>
public class MainMenuAction(IEnumerable<IMainMenuAction> menuActions) : ITuiAction
{
    public string Title => "Main Menu";

    public async Task ExecuteAsync()
    {
        // Add a "Quit" option dynamically
        var choices = menuActions.ToList<ITuiAction>();
        choices.Add(new BackAction("[Gray84]Exit[/]"));

        while (true)
        {
            TuiTheme.RenderHeader("WB Addon Manager");

            var selected = await TuiTheme.PromptSelectionOrDefaultAsync(
                "Choose actions (press [bold]Q[/] to return)",
                choices,
                action => action.Title);

            if (selected is null or BackAction)
                break;

            await selected.ExecuteSafelyAsync();
        }
    }
}