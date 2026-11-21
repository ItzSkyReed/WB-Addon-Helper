using System.Text.Json.Serialization;

namespace WbGenerator.DocGen.Documentation.Models;

/// <summary>
/// Represents a single advancement entry in the exported WorldBorder Addon JSON documentation.
/// </summary>
public class AdvancementDocEntry
{
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("description")] public required string Description { get; init; }
    [JsonPropertyName("frame")] public required string Frame { get; init; }
    [JsonPropertyName("icon_id")] public required string IconId { get; init; }
    [JsonPropertyName("player_head_data")] public PlayerHeadDocEntry? PlayerHeadData { get; init; }
    [JsonPropertyName("tier")] public required string Tier { get; init; }
    [JsonPropertyName("tab")] public required string Tab { get; init; }
    [JsonPropertyName("mc_path")] public required string McPath { get; init; }
    [JsonPropertyName("parent")] public ParentAdvancementDocEntry? Parent { get; init; }
    [JsonPropertyName("rewards")] public BlockRewardDocEntry? Rewards { get; init; }
}