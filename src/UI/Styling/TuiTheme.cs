using Spectre.Console;
using Spectre.Console.Rendering;

namespace UI.Styling;

/// <summary>
/// Centralized styling definitions for the TUI to ensure visual consistency.
/// </summary>
public static class TuiTheme
{
    public const string TablePropertyColor = "Grey70";
    private static readonly QKeyCancellableConsole CancellableConsole = new(AnsiConsole.Console);

    /// <summary>
    /// Renders a standardized selection prompt for reference types that returns <see langword="null"/>
    /// if the selection was aborted via the Q key.
    /// </summary>
    /// <typeparam name="T">The reference type of selectable elements.</typeparam>
    /// <param name="title">The title displayed above the options (supports Spectre.Console markup).</param>
    /// <param name="choices">The collection of available options.</param>
    /// <param name="displaySelector">An optional projection function for custom item labels.</param>
    /// <returns>
    /// The selected item of type <typeparamref name="T"/>, or <see langword="null"/> if the prompt was cancelled.
    /// </returns>
    /// <example>
    /// <code>
    /// var selectedUser = await TuiTheme.PromptSelectionOrDefaultAsync(
    ///     "Choose user:",
    ///     users,
    ///     u => u.Name);
    /// </code>
    /// </example>
    public static async Task<T?> PromptSelectionOrDefaultAsync<T>(
        string title,
        IEnumerable<T> choices,
        Func<T, string>? displaySelector = null) where T : class
    {
        var prompt = new SelectionPrompt<T>()
            .Title($"[yellow]{title}[/]")
            .PageSize(15)
            .AddChoices(choices);

        if (displaySelector != null)
            prompt.UseConverter(displaySelector);

        try
        {
            return await CancellableConsole.PromptAsync(prompt);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Renders a standardized selection prompt for value types (e.g., structs, enums) that returns
    /// <see langword="null"/> if the selection was aborted via the Q key.
    /// </summary>
    /// <typeparam name="T">The value type of selectable elements.</typeparam>
    /// <param name="title">The title displayed above the options (supports Spectre.Console markup).</param>
    /// <param name="choices">The collection of available options.</param>
    /// <param name="displaySelector">An optional projection function for custom item labels.</param>
    /// <returns>
    /// The selected value of type <typeparamref name="T"/> wrapped in <see cref="Nullable{T}"/>,
    /// or <see langword="null"/> if the prompt was cancelled.
    /// </returns>
    /// <example>
    /// <code>
    /// var option = await TuiTheme.PromptValueSelectionOrDefaultAsync(
    ///     "Choose option:",
    ///     Enum.GetValues&lt;AdvancementEditOption&gt;(),
    ///     opt => opt.ToString());
    /// </code>
    /// </example>
    public static async Task<T?> PromptValueSelectionOrDefaultAsync<T>(
        string title,
        IEnumerable<T> choices,
        Func<T, string>? displaySelector = null) where T : struct
    {
        var prompt = new SelectionPrompt<T>()
            .Title($"[yellow]{title}[/]")
            .PageSize(15)
            .AddChoices(choices);

        if (displaySelector != null)
            prompt.UseConverter(displaySelector);

        try
        {
            return await CancellableConsole.PromptAsync(prompt);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }


    public static void RenderHeader(string title)
    {
        AnsiConsole.Clear();
        var rule = new Rule($"[bold cyan]{title}[/]").LeftJustified();
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }

    public const string BackOptionString = "[Gray84]Back[/]";

    /// <summary>
    /// Displays a stylized alert panel with a header, rounded borders, and custom markup content.
    /// </summary>
    /// <param name="header">The title displayed on the top panel border (supports Spectre.Console markup).</param>
    /// <param name="content">The inner markup content of the panel.</param>
    /// <param name="borderColor">The border color. Defaults to red.</param>
    /// <example>
    /// <code>
    /// TuiTheme.ShowAlert("[bold red]Configuration Required[/]", "Please update settings.");
    /// </code>
    /// </example>
    public static void ShowAlert(string header, string content, Color? borderColor = null)
    {
        var panel = new Panel(new Markup(content))
        {
            Header = new PanelHeader(header),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor ?? Color.Yellow),
            Padding = new Padding(1, 1, 1, 1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public static void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] {message}");
    }

    public static void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[bold green]Success:[/] {message}");
    }

    /// <summary>
    /// Displays a standardized warning message in yellow text.
    /// </summary>
    /// <param name="message">The warning message to display.</param>
    public static void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]{message}[/]");
    }

    /// <summary>
    /// Displays a standardized informational message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="color">The markup color of the message. Defaults to <c>Gray84</c>.</param>
    public static void ShowInfo(string message, string color = "Gray84")
    {
        AnsiConsole.MarkupLine($"[{color}]{message}[/]");
    }

    /// <summary>
    /// Creates a standardized table with rounded borders and optional columns.
    /// </summary>
    /// <param name="columns">An array of column names to initialize the table with.</param>
    /// <returns>A configured <see cref="Table"/> instance.</returns>
    /// <example>
    /// <code>
    /// var table = TuiTheme.CreateTable("Datapack", "Count");
    /// </code>
    /// </example>
    public static Table CreateTable(params string[] columns) =>
        CreateTable(true, columns);

    /// <summary>
    /// Creates a standardized table with rounded borders and optional columns.
    /// </summary>
    /// <param name="bold">Make column headers bold or not</param>
    /// <param name="columns">An array of column names to initialize the table with.</param>
    /// <returns>A configured <see cref="Table"/> instance.</returns>
    /// <example>
    /// <code>
    /// var table = TuiTheme.CreateTable("Datapack", "Count");
    /// </code>
    /// </example>
    public static Table CreateTable(bool bold = true, params string[] columns)
    {
        var table = new Table().Border(TableBorder.Rounded);

        foreach (var column in columns)
            table.AddColumn(bold ? $"[bold]{column}[/]" : column);

        return table;
    }

    /// <summary>
    /// Creates a standardized tree component with the specified root node text.
    /// </summary>
    /// <param name="rootMarkup">The markup text for the root node of the tree.</param>
    /// <returns>A configured <see cref="Tree"/> instance.</returns>
    /// <example>
    /// <code>
    /// var tree = TuiTheme.CreateTree("[bold cyan]Root[/]");
    /// </code>
    /// </example>
    public static Tree CreateTree(string rootMarkup)
    {
        return new Tree(rootMarkup);
    }

    /// <summary>
    /// Renders any Spectre.Console renderable element to the terminal.
    /// </summary>
    /// <param name="element">The renderable element, such as a <see cref="Table"/> or <see cref="Tree"/>.</param>
    public static void RenderElement(IRenderable element)
    {
        AnsiConsole.Write(element);
    }

    /// <summary>
    /// Writes an empty line to the terminal for spacing.
    /// </summary>
    public static void Space()
    {
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders a standardized selection prompt.
    /// </summary>
    /// <typeparam name="T">The type of items to select.</typeparam>
    public static T PromptSelection<T>(string title, IEnumerable<T> choices, Func<T, string>? displaySelector = null) where T : notnull
    {
        var prompt = new SelectionPrompt<T>()
            .Title($"[yellow]{title}[/]")
            .PageSize(15)
            .AddChoices(choices);

        if (displaySelector != null)
            prompt.UseConverter(displaySelector);

        return AnsiConsole.Prompt(prompt);
    }

    public static void WaitForKey()
    {
        AnsiConsole.MarkupLine("\n[Gray84]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    /// <summary>
    /// Executes a synchronous action over a collection of items, displaying a standardized progress bar.
    /// </summary>
    /// <typeparam name="T">The type of items to process.</typeparam>
    /// <param name="description">The text displayed next to the progress bar.</param>
    /// <param name="items">The collection of items to process.</param>
    /// <param name="action">The action to perform on each item.</param>
    public static void RunProgress<T>(string description, IReadOnlyCollection<T> items, Action<T> action)
    {
        AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(ctx =>
            {
                var task = ctx.AddTask($"[green]{description}[/]", new ProgressTaskSettings
                {
                    MaxValue = items.Count
                });

                foreach (var item in items)
                {
                    action(item);
                    task.Increment(1);
                }
            });
    }
}