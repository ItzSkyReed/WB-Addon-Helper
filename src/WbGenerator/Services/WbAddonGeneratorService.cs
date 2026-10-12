using System.Text.Json;
using BacapGenerator.Advancements.Models;
using BacapGenerator.Datapacks;
using BacapGenerator.Utils;
using Microsoft.EntityFrameworkCore;
using WbGenerator.Enitites;
using WbGenerator.Exceptions;
using WbGenerator.Models;
using WbGenerator.Persistence;
using WbGenerator.Services.Interfaces;

namespace WbGenerator.Services;

/// <summary>
/// Coordinates the generation of WorldBorder addon files: initialization functions, fanpack function tags,
/// and individual advancement reward functions.
/// </summary>
/// <param name="dbContext">The SQLite database context providing block counts and tier overrides.</param>
/// <param name="registry">The registry containing loaded datapacks.</param>
/// <param name="rewardGenerator">The generator responsible for constructing .mcfunction reward files.</param>
public sealed class WbAddonGeneratorService(
    AppDbContext dbContext,
    DatapackRegistry registry,
    IWbRewardFunctionGenerator rewardGenerator) : IWbAddonGeneratorService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <inheritdoc />
    public async Task GenerateAsync(string targetDatapackPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatapackPath);

        // Preload database tables into dictionaries to avoid N+1 query overhead
        var indBlocks = await dbContext.IndividualBlockRewards
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Path, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var customTiers = await dbContext.TierCustomBlockRewards
            .AsNoTracking()
            .ToDictionaryAsync(x => x.McPath, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var tierOverrides = await dbContext.AdvancementTierOverrides
            .AsNoTracking()
            .ToDictionaryAsync(x => x.McPath, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Gather valid advancements from BACAP and BACAPED
        var targetPacks = registry.Values
            .Where(dp => dp.Id.Equals("bacap", StringComparison.OrdinalIgnoreCase) ||
                         dp.Id.Equals("bacaped", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var advancements = targetPacks
            .SelectMany(p => p.Advancements.OfType<BacapAdvancement>())
            .DistinctBy(a => a.McPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var bacapInitLines = new List<string>(advancements.Count);
        var bacapedInitLines = new List<string>(advancements.Count);

        foreach (var adv in advancements)
        {
            var cleanAdvPath = MinecraftUtils.StripNamespace(adv.McPath);
            var isExcluded = CheckIsExcluded(targetDatapackPath, cleanAdvPath);

            var rewardPath = adv.Advancement.Rewards?.Function ?? adv.McPath;
            var searchPath = MinecraftUtils.StripNamespace(rewardPath);

            indBlocks.TryGetValue(searchPath, out var indBlock);
            customTiers.TryGetValue(searchPath, out var customTier);
            tierOverrides.TryGetValue(searchPath, out var tierOverride);

            var inBacap = (indBlock?.BlocksBacap is not null) || isExcluded;
            var inBacaped = (indBlock?.BlocksBacaped is not null) || isExcluded;

            if (!inBacap && !inBacaped)
            {
                throw new WbRewardMissingException(adv.McPath, searchPath);
            }

            var cmdType = indBlock?.CommandType ?? "add";
            var (bacapInit, bacapedInit, tier, customBlocks) = CalculateTiersAndInitLines(
                adv, searchPath, indBlock, customTier, tierOverride, cmdType, inBacap, inBacaped, isExcluded);

            if (bacapInit is not null) bacapInitLines.Add(bacapInit);
            if (bacapedInit is not null) bacapedInitLines.Add(bacapedInit);

            // Generate individual reward function (via command models)
            var context = new RewardGenerationContext
            {
                Advancement = adv,
                CleanRewardPath = searchPath,
                CommandType = cmdType,
                Tier = tier,
                CustomBlocksValue = customBlocks,
                IsExcluded = isExcluded
            };

            await rewardGenerator.GenerateRewardFunctionAsync(context, targetDatapackPath, cancellationToken);

            // Write BACAP fanpack function tags linking advancement trigger to reward function
            await WriteFunctionTagsAsync(targetDatapackPath, searchPath, inBacap, inBacaped, cancellationToken);
        }

        // 3. Write init_blocks mcfunction files
        var initDirectory = Path.Combine(targetDatapackPath, "data", WbDefaults.WbNanespace, "function", "init_blocks", "individual");
        Directory.CreateDirectory(initDirectory);

        if (bacapInitLines.Count > 0)
        {
            await File.WriteAllLinesAsync(
                Path.Combine(initDirectory, "bacap.mcfunction"),
                bacapInitLines,
                cancellationToken);
        }

        if (bacapedInitLines.Count > 0)
        {
            await File.WriteAllLinesAsync(
                Path.Combine(initDirectory, "bacaped.mcfunction"),
                bacapedInitLines,
                cancellationToken);
        }
    }

    /// <summary>
    /// Checks whether an advancement has a pre-existing custom reward file in the destination datapack preset.
    /// </summary>
    /// <param name="targetDatapackPath">The destination directory of the datapack.</param>
    /// <param name="cleanAdvPath">The stripped advancement path.</param>
    /// <returns><see langword="true"/> if the preset file already exists; otherwise, <see langword="false"/>.</returns>
    private static bool CheckIsExcluded(string targetDatapackPath, string cleanAdvPath)
    {
        var presetFilePath = Path.Combine(
            targetDatapackPath,
            "data",
            WbDefaults.WbNanespace,
            "function",
            "rewards",
            $"{cleanAdvPath}.mcfunction");

        return File.Exists(presetFilePath);
    }

    /// <summary>
    /// Calculates scoreboard initialization command lines and resolves advancement tier values.
    /// </summary>
    private static (string? BacapInit, string? BacapedInit, string Tier, int? CustomBlocks) CalculateTiersAndInitLines(
        ValidAdvancement adv,
        string searchPath,
        IndividualBlockReward? indBlock,
        TierCustomBlockReward? customTier,
        AdvancementTierOverride? tierOverride,
        string cmdType,
        bool inBacap,
        bool inBacaped,
        bool isExcluded)
    {
        string? bacapInit = null;
        string? bacapedInit = null;

        var tier = adv is BacapAdvancement bacapAdv
            ? bacapAdv.Tier.ToString().ToLowerInvariant()
            : "tier1";

        int? customBlocksValue = null;

        if (inBacap && !isExcluded && indBlock?.BlocksBacap is not null)
        {
            var blocks = CalculateScoreboardBlocks(indBlock.BlocksBacap.Value, cmdType);
            bacapInit = $"scoreboard players set {searchPath} wb_adv_blocks {blocks}";

            if (!string.IsNullOrWhiteSpace(tierOverride?.Bacap))
                tier = tierOverride.Bacap;

            if (customTier?.Bacap is not null)
            {
                tier = "custom";
                customBlocksValue = (int)(customTier.Bacap.Value * 100);
            }
        }

        if (inBacaped && !isExcluded && indBlock?.BlocksBacaped is not null)
        {
            var blocks = CalculateScoreboardBlocks(indBlock.BlocksBacaped.Value, cmdType);
            bacapedInit = $"scoreboard players set {searchPath} wb_adv_blocks {blocks}";

            if (!string.IsNullOrWhiteSpace(tierOverride?.Bacaped))
                tier = tierOverride.Bacaped;

            if (customTier?.Bacaped is not null)
            {
                tier = "custom";
                customBlocksValue = (int)(customTier.Bacaped.Value * 100);
            }
        }

        return (bacapInit, bacapedInit, tier, customBlocksValue);
    }

    /// <summary>
    /// Calculates integer scoreboard units from float values based on the command type.
    /// </summary>
    private static int CalculateScoreboardBlocks(double rawBlocks, string cmdType) =>
        cmdType == "set" ? (int)rawBlocks : (int)(rawBlocks * 100);

    /// <summary>
    /// Writes fanpack tag JSON files linking BACAP triggers to WorldBorder reward functions.
    /// </summary>
    private static async Task WriteFunctionTagsAsync(
        string targetDatapackPath,
        string searchPath,
        bool inBacap,
        bool inBacaped,
        CancellationToken cancellationToken)
    {
        var tagPayload = new { values = new[] { $"bacap_wb_addon:rewards/{searchPath}" } };
        var jsonContent = JsonSerializer.Serialize(tagPayload, JsonOptions);

        if (inBacap)
        {
            var bacapTagPath = Path.Combine(
                targetDatapackPath, "data", "bacap_fanpacks", "tags", "function", $"{searchPath}.json");
            EnsureDirectoryExists(bacapTagPath);
            await File.WriteAllTextAsync(bacapTagPath, jsonContent, cancellationToken);
        }

        if (inBacaped)
        {
            var bacapedTagPath = Path.Combine(
                targetDatapackPath, "data", "bacaped_fanpacks", "tags", "function", $"{searchPath}.json");
            EnsureDirectoryExists(bacapedTagPath);
            await File.WriteAllTextAsync(bacapedTagPath, jsonContent, cancellationToken);
        }
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}