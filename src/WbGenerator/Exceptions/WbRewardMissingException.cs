namespace WbGenerator.Exceptions;

/// <summary>
/// Thrown when an advancement has no corresponding reward record configured in SQLite and is not excluded.
/// </summary>
/// <param name="advancementId">The identifier of the advancement that lacks configuration.</param>
/// <param name="rewardPath">The expected reward function path.</param>
public sealed class WbRewardMissingException(string advancementId, string rewardPath)
    : InvalidOperationException($"Advancement '{advancementId}' (reward: '{rewardPath}') is missing reward block data in SQLite database.")
{
    /// <summary>
    /// Gets the identifier of the faulty advancement.
    /// </summary>
    public string AdvancementId { get; } = advancementId;

    /// <summary>
    /// Gets the clean reward function path.
    /// </summary>
    public string RewardPath { get; } = rewardPath;
}