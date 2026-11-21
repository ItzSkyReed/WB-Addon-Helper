using System.Text.Json;
using BacapGenerator.Advancements.Models;
using BacapGenerator.Configuration;
using BacapGenerator.Datapacks;
using BacapGenerator.Datapacks.Models;
using BacapGenerator.Datapacks.Models.Settings;
using BacapGenerator.Utils;
using Core.DataComponents.Components;
using Core.Items;
using Core.Serialization;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using WbGenerator.DocGen.Documentation.Models;
using WbGenerator.Enitites;
using WbGenerator.Exceptions;
using WbGenerator.Persistence;
using WbGenerator.Services.Interfaces;

namespace WbGenerator.DocGen.Documentation.Services;

/// <summary>
/// Service responsible for aggregating configured datapack advancements with their SQLite WorldBorder
/// reward definitions and exporting them into a single JSON documentation file.
/// </summary>
/// <param name="dbContext">The EF Core database context providing reward, custom tier, and tier override tables.</param>
/// <param name="registry">The registry containing all loaded datapacks.</param>
/// <param name="rewardService">The service used to determine whether an advancement is excluded via preset functions.</param>
public sealed class WbDocumentationExportService(
    AppDbContext dbContext,
    DatapackRegistry registry,
    IWbRewardService rewardService)
{
    private const string PlayerHeadId = "player_head";

    /// <summary>
    /// Generates a unified documentation JSON file for the datapacks specified in <paramref name="config"/>
    /// (or all loaded reference datapacks when none are explicitly listed).
    /// </summary>
    /// <param name="config">The optional documentation generator configuration containing output path and datapack order.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="DocumentationExportResult"/> containing output path, counts, or failure/skip reason.</returns>
    /// <exception cref="WbRewardMissingException">
    /// Thrown when a non-excluded advancement has no reward data in the SQLite database for either edition.
    /// </exception>
    /// <example>
    /// <code>
    /// var result = await exportService.GenerateExportFileAsync(globalConfig.DocumentGenerator, ct);
    /// </code>
    /// </example>
    [PublicAPI]
    public async Task<DocumentationExportResult> GenerateExportFileAsync(
        DocumentGeneratorConfig? config,
        CancellationToken cancellationToken = default)
    {
        if (config is null)
            return DocumentationExportResult.Skipped("Section 'document_generator' is not configured.");

        config.Validate();

        var targetPacks = ResolveTargetDatapacks(config.Datapacks);
        if (targetPacks.Count == 0)
            return DocumentationExportResult.Skipped("No matching datapacks found in registry for documentation export.");

        var indBlocks = await dbContext.IndividualBlockRewards
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Path, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var customTiers = await dbContext.TierCustomBlockRewards
            .AsNoTracking()
            .ToDictionaryAsync(x => x.McPath, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var tierOverrides = await dbContext.AdvancementTierOverrides
            .AsNoTracking()
            .ToDictionaryAsync(x => x.McPath, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var advancements = targetPacks
            .SelectMany(p => p.Advancements.OfType<BacapAdvancement>())
            .DistinctBy(a => a.McPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.McPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var parentLookupPacks = BuildParentLookupOrder(targetPacks);
        var exportEntries = new List<AdvancementDocEntry>(advancements.Count);
        var skippedExcludedCount = 0;

        foreach (var adv in advancements)
        {
            var cleanAdvPath = MinecraftUtils.StripNamespace(adv.McPath);
            if (rewardService.IsExcluded(cleanAdvPath))
            {
                skippedExcludedCount++;
                continue;
            }

            var rewardPath = adv.Advancement.Rewards?.Function ?? adv.McPath;
            var searchPath = MinecraftUtils.StripNamespace(rewardPath);

            indBlocks.TryGetValue(searchPath, out var indBlock);
            customTiers.TryGetValue(searchPath, out var customTier);
            tierOverrides.TryGetValue(searchPath, out var tierOverride);

            var hasAnyReward = indBlock?.BlocksBacap is not null || indBlock?.BlocksBacaped is not null;
            if (!hasAnyReward)
                throw new WbRewardMissingException(adv.McPath, searchPath);

            exportEntries.Add(BuildEntry(adv, indBlock!, customTier, tierOverride, parentLookupPacks));
        }

        var outputDirectory = Path.GetFullPath(config.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, config.OutputFileName);

        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(
            stream,
            exportEntries,
            MinecraftDatapackJsonOptions.Documentation,
            cancellationToken);

        return DocumentationExportResult.Success(outputPath, exportEntries.Count, skippedExcludedCount);
    }

    /// <summary>
    /// Resolves target datapacks from <see cref="DatapackRegistry"/> in the configured priority order,
    /// or falls back to all <see cref="DatapackType.Reference"/> datapacks when no explicit IDs are configured.
    /// </summary>
    /// <param name="configuredIds">The ordered list of datapack identifiers from configuration.</param>
    /// <returns>An ordered list of resolved <see cref="Datapack"/> instances.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when one or more configured datapack IDs do not exist in the registry.
    /// </exception>
    private IReadOnlyList<Datapack> ResolveTargetDatapacks(IReadOnlyList<string> configuredIds)
    {
        if (configuredIds.Count == 0)
        {
            return registry.Values
                .Where(dp => dp.Settings.DatapackType == DatapackType.Reference)
                .OrderBy(dp => dp.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var byId = registry.Values.ToDictionary(dp => dp.Id, StringComparer.OrdinalIgnoreCase);
        var resolved = new List<Datapack>(configuredIds.Count);
        var missingIds = new List<string>();

        foreach (var id in configuredIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (byId.TryGetValue(id, out var datapack))
                resolved.Add(datapack);
            else
                missingIds.Add(id);
        }

        if (missingIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configured documentation datapack(s) not found in registry: {string.Join(", ", missingIds)}.");
        }

        return resolved;
    }

    /// <summary>
    /// Builds a deterministic datapack lookup sequence for parent resolution,
    /// prioritizing configured target datapacks followed by remaining loaded packs.
    /// </summary>
    /// <param name="targetPacks">The already prioritized target datapacks.</param>
    /// <returns>The full prioritized list of datapacks for parent advancement lookup.</returns>
    private IReadOnlyList<Datapack> BuildParentLookupOrder(IReadOnlyList<Datapack> targetPacks)
    {
        var targetIds = targetPacks
            .Select(dp => dp.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var remainingPacks = registry.Values
            .Where(dp => !targetIds.Contains(dp.Id))
            .OrderBy(dp => dp.Settings.DatapackType)
            .ThenBy(dp => dp.Id, StringComparer.OrdinalIgnoreCase);

        return [.. targetPacks, .. remainingPacks];
    }

    /// <summary>
    /// Maps an internal <see cref="BacapAdvancement"/> and its associated WorldBorder database records
    /// into a public <see cref="AdvancementDocEntry"/> documentation model.
    /// </summary>
    /// <param name="advancement">The source advancement model.</param>
    /// <param name="indBlock">The individual block reward record from SQLite.</param>
    /// <param name="customTier">The optional custom tier block reward override from SQLite.</param>
    /// <param name="tierOverride">The optional tier name override from SQLite.</param>
    /// <param name="parentLookupPacks">The prioritized list of datapacks used to resolve parent metadata.</param>
    /// <returns>A populated <see cref="AdvancementDocEntry"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="advancement"/> or <paramref name="indBlock"/> is null.
    /// </exception>
    private static AdvancementDocEntry BuildEntry(
        BacapAdvancement advancement,
        IndividualBlockReward indBlock,
        TierCustomBlockReward? customTier,
        AdvancementTierOverride? tierOverride,
        IReadOnlyList<Datapack> parentLookupPacks)
    {
        ArgumentNullException.ThrowIfNull(advancement);
        ArgumentNullException.ThrowIfNull(indBlock);

        var display = advancement.Advancement.Display!;
        var icon = display.Icon!;

        return new AdvancementDocEntry
        {
            McPath = advancement.McPath,
            Title = advancement.TitleText,
            Frame = display.Frame.ToString().ToLowerInvariant(),
            Description = advancement.CleanDescriptionText,
            IconId = MinecraftUtils.EnsureNamespace(icon.Id),
            Tier = advancement.Tier.TechnicalName,
            Tab = advancement.Tab.FolderName,
            Parent = BuildParent(advancement, parentLookupPacks),
            PlayerHeadData = TryBuildPlayerHeadData(icon),
            Rewards = BuildBlockReward(indBlock, customTier, tierOverride)
        };
    }

    /// <summary>
    /// Builds the block reward documentation model from individual reward, custom tier, and tier override records.
    /// </summary>
    /// <param name="indBlock">The individual block reward entity.</param>
    /// <param name="customTier">The optional custom tier block reward entity.</param>
    /// <param name="tierOverride">The optional tier override entity.</param>
    /// <returns>A populated <see cref="BlockRewardDocEntry"/> instance.</returns>
    private static BlockRewardDocEntry BuildBlockReward(
        IndividualBlockReward indBlock,
        TierCustomBlockReward? customTier,
        AdvancementTierOverride? tierOverride)
    {
        return new BlockRewardDocEntry
        {
            CommandType = indBlock.CommandType,
            BlocksBacap = indBlock.BlocksBacap,
            BlocksBacaped = indBlock.BlocksBacaped,
            BacapCustomTierReward = customTier?.Bacap,
            BacapedCustomTierReward = customTier?.Bacaped,
            BacapTierOverride = string.IsNullOrWhiteSpace(tierOverride?.Bacap) ? null : tierOverride.Bacap,
            BacapedTierOverride = string.IsNullOrWhiteSpace(tierOverride?.Bacaped) ? null : tierOverride.Bacaped
        };
    }

    /// <summary>
    /// Builds the aggregated documentation model for an advancement's parent by searching loaded datapacks.
    /// </summary>
    /// <param name="advancement">The source advancement containing the parent identifier.</param>
    /// <param name="sortedDatapacks">The prioritized list of loaded datapacks to search within.</param>
    /// <returns>
    /// A populated <see cref="ParentAdvancementDocEntry"/> instance if <see cref="BacapAdvancement.Parent"/> is defined;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    private static ParentAdvancementDocEntry? BuildParent(
        BacapAdvancement advancement,
        IReadOnlyList<Datapack> sortedDatapacks)
    {
        var parentId = advancement.Parent;
        if (parentId is null)
            return null;

        var parentDoc = new ParentAdvancementDocEntry
        {
            McPath = parentId
        };

        foreach (var datapack in sortedDatapacks)
        {
            if (!datapack.TryGetAdvancement(parentId, out var parentManagedAdv) ||
                parentManagedAdv is not BacapAdvancement parent)
                continue;

            var display = parent.Advancement.Display!;
            var icon = display.Icon!;

            parentDoc.Title = parent.TitleText;
            parentDoc.Description = parent.CleanDescriptionText;
            parentDoc.Tier = parent.Tier.TechnicalName;
            parentDoc.Frame = display.Frame.ToString().ToLowerInvariant();
            parentDoc.IconId = MinecraftUtils.EnsureNamespace(icon.Id);
            parentDoc.PlayerHeadData = TryBuildPlayerHeadData(icon);

            break;
        }

        return parentDoc;
    }

    /// <summary>
    /// Attempts to extract player head profile metadata if the item stack represents a <c>player_head</c>.
    /// </summary>
    /// <param name="itemStack">The item stack to inspect.</param>
    /// <returns>
    /// A populated <see cref="PlayerHeadDocEntry"/> if the item is a player head with a <see cref="ProfileComponent"/>;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    private static PlayerHeadDocEntry? TryBuildPlayerHeadData(ItemStack itemStack)
    {
        if (!MinecraftUtils.StripNamespace(itemStack.Id).Equals(PlayerHeadId, StringComparison.OrdinalIgnoreCase))
            return null;

        return !itemStack.Components.TryGet<ProfileComponent>(out var profileComponent) ? null : BuildPlayerHeadData(profileComponent);
    }

    /// <summary>
    /// Maps a Minecraft <see cref="ProfileComponent"/> to a <see cref="PlayerHeadDocEntry"/>.
    /// </summary>
    /// <param name="profileComponent">The profile component attached to the player head item.</param>
    /// <returns>A populated <see cref="PlayerHeadDocEntry"/> containing the player UUID and extracted texture hash.</returns>
    private static PlayerHeadDocEntry BuildPlayerHeadData(ProfileComponent profileComponent)
    {
        var textureBase64 = profileComponent.Properties?.FirstOrDefault()?.Value;
        string? textureHash = null;

        if (!string.IsNullOrEmpty(textureBase64))
            textureHash = MinecraftUtils.ExtractTextureHash(textureBase64);

        return new PlayerHeadDocEntry
        {
            Uuid = profileComponent.Id,
            TextureHash = textureHash
        };
    }
}