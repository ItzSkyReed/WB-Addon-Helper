using WbGenerator.Enitites;
using WbGenerator.Models;

namespace WbGenerator.Services.Interfaces;

/// <summary>
/// Provides operations for managing WorldBorder advancement rewards stored in SQLite and validating against datapack files.
/// </summary>
public interface IWbRewardService
{
    /// <summary>
    /// Determines whether the advancement has a pre-existing custom reward function in the addon directory and should be excluded from auto-generation.
    /// </summary>
    /// <param name="advancementPath">The advancement identifier without namespace (e.g., "story/root").</param>
    /// <returns><see langword="true"/> if the custom reward function file exists; otherwise, <see langword="false"/>.</returns>
    bool IsExcluded(string advancementPath);

    /// <summary>
    /// Retrieves the reward record for a given advancement reward path.
    /// </summary>
    /// <param name="cleanPath">The reward path without namespace.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="IndividualBlockReward"/> entity if found; otherwise, <see langword="null"/>.</returns>
    Task<IndividualBlockReward?> GetRewardAsync(string cleanPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an advancement is missing reward block configuration for the specified target edition.
    /// </summary>
    /// <param name="record">The existing database record, or <see langword="null"/> if not yet present.</param>
    /// <param name="target">The target edition (<see cref="WbTarget.Bacap"/> or <see cref="WbTarget.Bacaped"/>).</param>
    /// <returns><see langword="true"/> if the reward block value is not configured; otherwise, <see langword="false"/>.</returns>
    bool IsMissing(IndividualBlockReward? record, WbTarget target);

    /// <summary>
    /// Creates or updates an individual block reward entry in the database.
    /// </summary>
    /// <param name="cleanPath">The clean advancement reward path without namespace.</param>
    /// <param name="commandType">The scoreboard command type: "add" or "set".</param>
    /// <param name="blocks">The number of blocks configured.</param>
    /// <param name="target">The target edition being updated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="commandType"/> is invalid or block limit is exceeded.</exception>
    Task SaveMissingRewardAsync(
        string cleanPath,
        string commandType,
        double blocks,
        WbTarget target,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the Minecraft namespace prefix from an identifier.
    /// </summary>
    /// <param name="resourceLocation">The raw Minecraft resource location (e.g. "bacap:story/root").</param>
    /// <returns>The path without the namespace prefix (e.g. "story/root").</returns>
    string StripNamespace(string resourceLocation);
}