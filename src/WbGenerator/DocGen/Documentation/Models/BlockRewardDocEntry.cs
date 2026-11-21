using System.Text.Json.Serialization;

namespace WbGenerator.DocGen.Documentation.Models;

/// <summary>
/// Represents WorldBorder block reward rules, custom tier rewards, and tier overrides
/// for both BACAP and BACAPED editions.
/// </summary>
public class BlockRewardDocEntry
{
    [JsonPropertyName("command_type")] public required string CommandType { get; init; }
    [JsonPropertyName("blocks_bacap")] public double? BlocksBacap { get; init; }
    [JsonPropertyName("blocks_bacaped")] public double? BlocksBacaped { get; init; }
    [JsonPropertyName("bacap_custom_tier_reward")] public double? BacapCustomTierReward { get; init; }
    [JsonPropertyName("bacaped_custom_tier_reward")] public double? BacapedCustomTierReward { get; init; }
    [JsonPropertyName("bacap_tier_override")] public string? BacapTierOverride { get; init; }
    [JsonPropertyName("bacaped_tier_override")] public string? BacapedTierOverride { get; init; }
}