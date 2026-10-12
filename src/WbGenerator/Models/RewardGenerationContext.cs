using BacapGenerator.Advancements.Models;

namespace WbGenerator.Models;

/// <summary>
/// Encapsulates all resolved advancement metadata, scoreboard types, and tier overrides
/// required to construct a reward .mcfunction file.
/// </summary>
public sealed record RewardGenerationContext
{
    /// <summary>
    /// Gets the target advancement instance.
    /// </summary>
    public required BacapAdvancement Advancement { get; init; }

    /// <summary>
    /// Gets the clean reward function identifier (without namespace prefix).
    /// </summary>
    public required string CleanRewardPath { get; init; }

    /// <summary>
    /// Gets the scoreboard operation mode ("add" or "set").
    /// </summary>
    public required string CommandType { get; init; }

    /// <summary>
    /// Gets the resolved advancement tier string name (e.g. "tier1", "custom").
    /// </summary>
    public required string Tier { get; init; }

    /// <summary>
    /// Gets the custom block count in hundredths of blocks if the tier is set to "custom".
    /// </summary>
    public int? CustomBlocksValue { get; init; }

    /// <summary>
    /// Gets a value indicating whether this reward function was already provided by a custom preset file.
    /// </summary>
    public bool IsExcluded { get; init; }
}