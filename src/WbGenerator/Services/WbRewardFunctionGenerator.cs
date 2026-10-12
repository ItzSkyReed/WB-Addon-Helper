using BacapGenerator.Advancements.Models;
using BacapGenerator.Datapacks.Models;
using Core.McFunctions.Models;
using Core.McFunctions.Models.Interfaces;
using Core.SNBT;
using WbGenerator.Models;
using WbGenerator.Services.Interfaces;

namespace WbGenerator.Services;

/// <summary>
/// Synthesizes individual WorldBorder advancement reward .mcfunction files
/// using strongly typed Minecraft commands and SNBT builder.
/// </summary>
public sealed class WbRewardFunctionGenerator : IWbRewardFunctionGenerator
{
    /// <inheritdoc />
    public async Task GenerateRewardFunctionAsync(
        RewardGenerationContext context,
        string datapackRootDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(datapackRootDirectory);

        // Skip generating if the reward file was already provided as an excluded custom preset
        if (context.IsExcluded)
            return;

        var function = BuildRewardFunction(context);

        var targetFilePath = Path.Combine(
            datapackRootDirectory,
            "data",
            WbDefaults.WbNanespace,
            "function",
            "rewards",
            $"{context.CleanRewardPath}.mcfunction");

        var directoryPath = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var functionContent = string.Join("\n", function.Lines.Select(l => l.Build())) + "\n";
        await File.WriteAllTextAsync(targetFilePath, functionContent, cancellationToken);
    }

    /// <summary>
    /// Builds the structured <see cref="McFunction"/> containing the execution guard, storage payload assignment, and dispatch function.
    /// </summary>
    /// <param name="context">The contextual advancement and reward metadata.</param>
    /// <returns>A populated <see cref="McFunction"/> containing formatted raw command lines.</returns>
    private static McFunction BuildRewardFunction(RewardGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Resolve advancement presentation metadata directly from BACAP models
        var (title, desc, tab, titleColor, descColor) = ExtractAdvancementMetadata(
            context.Advancement);

        // Build SNBT payload compound with proper automatic string and character escaping
        var snbtBuilder = new SnbtCompoundBuilder()
            .Put("adv_id", context.CleanRewardPath)
            .Put("title", title)
            .Put("title_color", titleColor)
            .Put("desc", desc)
            .Put("desc_color", descColor)
            .Put("tab", tab)
            .Put("type", context.CommandType)
            .Put("tier", context.Tier);

        // Add integer custom_tier_blocks only if present (custom tier block reward pattern)
        if (context.CustomBlocksValue.HasValue)
            snbtBuilder.Put("custom_tier_blocks", context.CustomBlocksValue.Value);

        var storagePayload = snbtBuilder.Build();

        var lines = new List<IMcFunctionLine>(3)
        {
            new RawCommandLine(
                $"execute if score {context.CleanRewardPath} {WbDefaults.WbScoreboardObjective} matches 1 run return 0"),

            new RawCommandLine(
                $"data modify storage {WbDefaults.StorageTarget} {WbDefaults.StoragePath} set value {storagePayload.ToSnbtString()}"),

            new RawCommandLine(
                $"function {WbDefaults.ProcessRewardFunction} with storage {WbDefaults.StorageTarget} {WbDefaults.StoragePath}")
        };

        return new McFunction(lines);
    }



    /// <summary>
    /// Extracts display text, tab identifier, title color, and description color from the advancement.
    /// </summary>
    /// <param name="advancement">The target valid advancement instance.</param>
    /// <returns>A tuple containing resolved display text and color metadata.</returns>
    private static (string Title, string Description, string Tab, string TitleColor, string DescColor) ExtractAdvancementMetadata(
        BacapAdvancement advancement)
    {
        var msgSettings = advancement.Tier.GetDefaultMessage();

        return (
            Title: advancement.TitleText,
            Description: advancement.CleanDescriptionText,
            Tab: advancement.Tab.DisplayName,
            msgSettings.TitleColor,
            DescColor: msgSettings.DescriptionColor
        );
    }
}