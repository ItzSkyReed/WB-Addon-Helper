using System.Globalization;
using BacapGenerator.Advancements.Models;
using BacapGenerator.Datapacks;
using BacapGenerator.Datapacks.Models;
using BacapGenerator.Utils;
using Core.Registries;
using Spectre.Console;
using UI.Interfaces;
using UI.Styling;
using WbGenerator.Enitites;
using WbGenerator.Models;
using WbGenerator.Services;
using WbGenerator.Services.Interfaces;

namespace UI.Menus;

/// <summary>
/// Interactive TUI action that inspects loaded datapacks for missing WorldBorder advancement
/// block rewards and prompts the user to configure them into SQLite.
/// </summary>
/// <param name="registry">The registry containing loaded Minecraft datapacks.</param>
/// <param name="minecraftData">The global registry for resolving Minecraft item display names.</param>
/// <param name="rewardService">The WorldBorder reward database and exclusion service.</param>
public class AddMissingRewardsMenu(
    DatapackRegistry registry,
    MinecraftData minecraftData,
    IWbRewardService rewardService) : IMainMenuAction
{
    public string Title => "Add Missing Rewards to DB";

    /// <summary>
    /// Executes the interactive search loop scanning for unconfigured advancement rewards and prompts for user input.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteAsync()
    {
        TuiTheme.RenderHeader("Add Missing WorldBorder Rewards");

        var target = PromptTargetEdition();
        if (target is null)
            return;

        var targetPacks = ResolveTargetDatapacks(target.Value);
        if (targetPacks.Count == 0)
        {
            TuiTheme.ShowWarning("No relevant datapacks found in registry for this edition.");
            TuiTheme.WaitForKey();
            return;
        }

        var bacapAdvancements = targetPacks
            .SelectMany(dp => dp.Advancements.OfType<BacapAdvancement>())
            .ToList();

        if (bacapAdvancements.Count == 0)
        {
            TuiTheme.ShowWarning("No valid advancements found across target datapacks.");
            TuiTheme.WaitForKey();
            return;
        }

        var processedCount = 0;
        var skippedCount = 0;

        foreach (var adv in bacapAdvancements)
        {
            var cleanAdvPath = MinecraftUtils.StripNamespace(adv.McPath);

            // Skip advancements having custom preset reward functions
            if (rewardService.IsExcluded(cleanAdvPath))
            {
                skippedCount++;
                continue;
            }

            var rewardPath = adv.Advancement.Rewards?.Function ?? adv.McPath;
            var cleanRewardPath = MinecraftUtils.StripNamespace(rewardPath);

            var record = await rewardService.GetRewardAsync(cleanRewardPath);

            if (!rewardService.IsMissing(record, target.Value))
                continue;

            RenderMissingAdvancementCard(adv, cleanRewardPath, target.Value, record);

            var promptResult = await PromptAndSaveRewardAsync(cleanRewardPath, record, target.Value);

            if (promptResult == PromptStatus.Abort)
            {
                TuiTheme.ShowWarning("Process aborted by user.");
                break;
            }

            if (promptResult == PromptStatus.Saved)
            {
                processedCount++;
            }
        }

        Console.WriteLine();
        TuiTheme.ShowSuccess($"Session finished! Processed: {processedCount}, Excluded preset files: {skippedCount}.");
        TuiTheme.WaitForKey();
    }

    /// <summary>
    /// Renders a detailed Spectre property card for the advancement requiring configuration.
    /// </summary>
    /// <param name="adv">The valid advancement being inspected.</param>
    /// <param name="cleanRewardPath">The stripped reward function identifier.</param>
    /// <param name="target">The target edition being configured.</param>
    /// <param name="existingRecord">The existing database record if available.</param>
    private void RenderMissingAdvancementCard(
        ValidAdvancement adv,
        string cleanRewardPath,
        WbTarget target,
        IndividualBlockReward? existingRecord)
    {
        TuiTheme.RenderHeader($"Missing Reward: {adv.McPath}");

        var table = TuiTheme.CreateTable("Property", "Value");

        table.AddRow($"[{TuiTheme.TablePropertyColor}]Target Edition[/]", $"[bold yellow]{target}[/]");
        table.AddRow($"[{TuiTheme.TablePropertyColor}]Datapack[/]", $"[cyan]{Markup.Escape(adv.Datapack.Id)}[/]");
        table.AddRow($"[{TuiTheme.TablePropertyColor}]McPath (File)[/]", $"[white]{Markup.Escape(adv.McPath)}[/]");
        table.AddRow($"[{TuiTheme.TablePropertyColor}]Reward McPath[/]", $"[yellow]{Markup.Escape(cleanRewardPath)}[/]");

        // Parent hierarchy formatting
        var parentMcPath = adv.Advancement.Parent;
        var parentAdvancement = !string.IsNullOrWhiteSpace(parentMcPath)
            ? adv.Datapack.Advancements
                .OfType<BacapAdvancement>()
                .FirstOrDefault(candidate => candidate.McPath == parentMcPath)
            : null;

        var parentColor = parentAdvancement?.Tier.Color() ?? "Grey84";
        var parentText = Markup.Escape(parentMcPath ?? "None (Root)");
        table.AddRow($"[{TuiTheme.TablePropertyColor}]Parent[/]", $"[{parentColor}]{parentText}[/]");

        if (adv is BacapAdvancement bacap)
        {
            table.AddRow($"[{TuiTheme.TablePropertyColor}]Title[/]", $"[white]{Markup.Escape(bacap.TitleText)}[/]");

            var descText = !string.IsNullOrWhiteSpace(bacap.CleanDescriptionText)
                ? Markup.Escape(bacap.CleanDescriptionText)
                : "[Gray84]None[/]";
            table.AddRow($"[{TuiTheme.TablePropertyColor}]Description[/]", $"[white]{descText}[/]");

            table.AddRow(
                $"[{TuiTheme.TablePropertyColor}]Tab[/]",
                $"[{bacap.Tab.Color}]■[/] [{bacap.Tab.Color}]{Markup.Escape(bacap.Tab.DisplayName)}[/] [Gray84]({bacap.Tab.FolderName})[/]");
            table.AddRow($"[{TuiTheme.TablePropertyColor}]Tier[/]", $"[{bacap.Tier.Color()}]{bacap.Tier.DisplayName()}[/]");

            // Existing item rewards overview
            var items = bacap.ItemRewardFunction.RewardItems;
            if (items.Count > 0)
            {
                var formattedItems = string.Join(", ", items.Select(item =>
                {
                    var cleanItemId = MinecraftUtils.StripNamespace(item.Id);
                    var displayName = ResolveItemDisplayName(cleanItemId);
                    return $"[green]{item.Count}x[/] [cyan]{Markup.Escape(displayName)}[/]";
                }));
                table.AddRow($"[{TuiTheme.TablePropertyColor}]Pack Item Rewards[/]", formattedItems);
            }
        }
        else
        {
            table.AddRow($"[{TuiTheme.TablePropertyColor}]DatapackType[/]", "[Gray84]Technical Trigger[/]");
        }

        // Database status summary
        var dbState = existingRecord is null
            ? "[red]Not in Database[/]"
            : $"[yellow]Registered (cmd: {existingRecord.CommandType}, " +
              $"bacap: {existingRecord.BlocksBacap?.ToString(CultureInfo.InvariantCulture) ?? "null"}, " +
              $"bacaped: {existingRecord.BlocksBacaped?.ToString(CultureInfo.InvariantCulture) ?? "null"})[/]";
        table.AddRow($"[{TuiTheme.TablePropertyColor}]DB Record Status[/]", dbState);

        TuiTheme.RenderElement(table);
    }

    /// <summary>
    /// Interactively queries the user for the command type via a selection menu and prompts for the block value via direct text input, persisting the result to SQLite.
    /// </summary>
    /// <param name="cleanPath">The clean advancement reward path without namespace.</param>
    /// <param name="record">The current database record if present.</param>
    /// <param name="target">The target pack edition being configured.</param>
    /// <returns>A <see cref="PromptStatus"/> indicating whether the reward was saved, skipped, or the workflow was aborted.</returns>
    private async Task<PromptStatus> PromptAndSaveRewardAsync(
        string cleanPath,
        IndividualBlockReward? record,
        WbTarget target)
    {
        var escapedPath = Markup.Escape(cleanPath);
        var commandType = record?.CommandType;

        // 1. Select command type via SelectionPrompt if not yet established in the database
        if (string.IsNullOrWhiteSpace(commandType))
        {
            var typePrompt = new SelectionPrompt<string>()
                .Title($"Select command type for [yellow]{escapedPath}[/]:")
                .PageSize(5)
                .AddChoices(
                    "add - Increment border size",
                    "set - Set absolute border size",
                    "Skip",
                    "Abort");

            var typeChoice = AnsiConsole.Prompt(typePrompt);

            if (typeChoice == "Abort")
                return PromptStatus.Abort;

            if (typeChoice == "Skip")
                return PromptStatus.Skipped;

            commandType = typeChoice.StartsWith("add", StringComparison.OrdinalIgnoreCase) ? "add" : "set";
        }

        var escapedCommand = Markup.Escape(commandType);

        // 2. Direct block value input via TextPrompt
        var blockPrompt = new TextPrompt<string>(
                $"Enter blocks count for [yellow]{escapedPath}[/] ([cyan]{escapedCommand}[/]) [grey](or type 'skip'/'abort')[/]:")
            .Validate(input =>
            {
                var trimmed = input.Trim();

                if (trimmed.Equals("skip", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("abort", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("q", StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.Success();
                }

                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                    double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                {
                    if (commandType == "add" && value > WbRewardService.MaxAddBlocksLimit)
                    {
                        return ValidationResult.Error(
                            $"Value exceeds maximum limit of {WbRewardService.MaxAddBlocksLimit:N0} for 'add' command.");
                    }

                    return ValidationResult.Success();
                }

                return ValidationResult.Error("Please enter a valid number (e.g. 0.5, 5, 20) or 'skip'/'abort'.");
            });

        // If configuring BACAPed and a BACAP value already exists, prefill it as default
        if (target == WbTarget.Bacaped && record?.BlocksBacap is not null)
        {
            blockPrompt.DefaultValue(record.BlocksBacap.Value.ToString(CultureInfo.InvariantCulture));
        }

        var blockInput = AnsiConsole.Prompt(blockPrompt).Trim();

        if (blockInput.Equals("abort", StringComparison.OrdinalIgnoreCase) ||
            blockInput.Equals("q", StringComparison.OrdinalIgnoreCase))
        {
            return PromptStatus.Abort;
        }

        if (blockInput.Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            return PromptStatus.Skipped;
        }

        var blocks = double.TryParse(blockInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue)
            ? parsedValue
            : double.Parse(blockInput, NumberStyles.Float, CultureInfo.CurrentCulture);

        // 3. Persist into SQLite
        try
        {
            await rewardService.SaveMissingRewardAsync(cleanPath, commandType, blocks, target);
            TuiTheme.ShowSuccess($"Saved [yellow]{escapedPath}[/] -> [green]{blocks}[/] ([cyan]{escapedCommand}[/])");
            return PromptStatus.Saved;
        }
        catch (Exception ex)
        {
            TuiTheme.ShowError($"Database error: {Markup.Escape(ex.Message)}");
            return PromptStatus.Abort;
        }
    }


    /// <summary>
    /// Displays a selection prompt to choose which target pack edition requires reward configuration.
    /// </summary>
    /// <returns>The selected <see cref="WbTarget"/>, or <see langword="null"/> if cancelled.</returns>
    private static WbTarget? PromptTargetEdition()
    {
        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select target database edition to populate:")
                .PageSize(5)
                .AddChoices("BACAP", "BACAPED", "Cancel"));

        return selection switch
        {
            "BACAP" => WbTarget.Bacap,
            "BACAPED" => WbTarget.Bacaped,
            _ => null
        };
    }

    /// <summary>
    /// Resolves the exact datapacks required for the target edition by their explicit identifier.
    /// </summary>
    /// <param name="target">The target pack edition being configured.</param>
    /// <returns>A list of matching datapacks present in the registry.</returns>
    private IReadOnlyList<Datapack> ResolveTargetDatapacks(WbTarget target)
    {
        // Exact IDs matching the Python dataset logic:
        // 'bacap' only for Bacap target; 'bacap' and 'bacaped' for Bacaped target.
        string[] targetIds = target switch
        {
            WbTarget.Bacap => ["bacap"],
            WbTarget.Bacaped => ["bacap", "bacaped"],
            _ => []
        };

        var resolvedPacks = new List<Datapack>(targetIds.Length);

        foreach (var id in targetIds)
        {
            var pack = registry.Values.FirstOrDefault(dp =>
                string.Equals(dp.Id, id, StringComparison.OrdinalIgnoreCase));

            if (pack is not null)
            {
                resolvedPacks.Add(pack);
            }
            else
            {
                TuiTheme.ShowWarning($"Datapack with ID '{Markup.Escape(id)}' was not found in the loaded registry.");
            }
        }

        return resolvedPacks;
    }

    /// <summary>
    /// Resolves the friendly display name of a Minecraft item from registry data.
    /// </summary>
    /// <param name="cleanItemId">The stripped item identifier.</param>
    /// <returns>The resolved item display name, or the clean ID if absent.</returns>
    private string ResolveItemDisplayName(string cleanItemId)
    {
        return minecraftData.Items.TryGetValue(cleanItemId, out var item)
            ? item.DisplayName
            : cleanItemId;
    }

    /// <summary>
    /// Internal status for controlling the prompt loop.
    /// </summary>
    private enum PromptStatus
    {
        Saved,
        Skipped,
        Abort
    }
}