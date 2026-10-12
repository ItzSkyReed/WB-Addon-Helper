using BacapGenerator.Configuration;
using Microsoft.EntityFrameworkCore;
using WbGenerator.Enitites;
using WbGenerator.Models;
using WbGenerator.Persistence;
using WbGenerator.Services.Interfaces;

namespace WbGenerator.Services;

/// <summary>
/// Service managing reward entities in SQLite and checking datapack exclusions.
/// </summary>
/// <param name="dbContext">The database context configured for SQLite.</param>
/// <param name="config">The global application configuration containing file paths.</param>
public sealed class WbRewardService(AppDbContext dbContext, GlobalConfig config) : IWbRewardService
{
    /// <summary>
    /// Scoreboard maximum limit for "add" command type before multiplying by 100 to avoid 32-bit signed integer overflow.
    /// Max signed 32-bit int is 2,147,483,647.
    /// </summary>
    public const double MaxAddBlocksLimit = 21_474_836.0;

    /// <inheritdoc />
    public bool IsExcluded(string advancementPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(advancementPath);

        // Path structure matches Python: data/bacap_wb_addon/function/rewards/{adv_path}.mcfunction
        var fullPath = Path.Combine(
            config.WbAddonPath,
            "data",
            "bacap_wb_addon",
            "function",
            "rewards",
            $"{advancementPath}.mcfunction");

        return File.Exists(fullPath);
    }

    /// <inheritdoc />
    public async Task<IndividualBlockReward?> GetRewardAsync(string cleanPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cleanPath);

        return await dbContext.IndividualBlockRewards
            .FirstOrDefaultAsync(x => x.Path == cleanPath, cancellationToken);
    }

    /// <inheritdoc />
    public bool IsMissing(IndividualBlockReward? record, WbTarget target)
    {
        if (record is null)
            return true;

        return target switch
        {
            WbTarget.Bacap => record.BlocksBacap is null,
            WbTarget.Bacaped => record.BlocksBacaped is null,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported target edition.")
        };
    }

    /// <inheritdoc />
    public async Task SaveMissingRewardAsync(
        string cleanPath,
        string commandType,
        double blocks,
        WbTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cleanPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);

        var normalizedCommandType = commandType.ToLowerInvariant();
        if (normalizedCommandType is not ("add" or "set"))
        {
            throw new ArgumentException("Command type must be either 'add' or 'set'.", nameof(commandType));
        }

        if (normalizedCommandType == "add" && blocks > MaxAddBlocksLimit)
        {
            throw new ArgumentException(
                $"Block amount for 'add' command exceeds maximum allowed threshold of {MaxAddBlocksLimit:N0}.",
                nameof(blocks));
        }

        var record = await dbContext.IndividualBlockRewards
            .FirstOrDefaultAsync(x => x.Path == cleanPath, cancellationToken);

        if (record is null)
        {
            record = new IndividualBlockReward
            {
                Path = cleanPath,
                CommandType = normalizedCommandType
            };
            dbContext.IndividualBlockRewards.Add(record);
        }
        else
        {
            record.CommandType = normalizedCommandType;
        }

        if (target == WbTarget.Bacap)
        {
            record.BlocksBacap = blocks;
        }
        else
        {
            record.BlocksBacaped = blocks;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public string StripNamespace(string resourceLocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceLocation);

        var colonIndex = resourceLocation.IndexOf(':');
        return colonIndex >= 0 ? resourceLocation[(colonIndex + 1)..] : resourceLocation;
    }
}