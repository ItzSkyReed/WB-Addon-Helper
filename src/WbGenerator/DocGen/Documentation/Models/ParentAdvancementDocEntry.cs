using System.Text.Json.Serialization;

namespace WbGenerator.DocGen.Documentation.Models;

public class ParentAdvancementDocEntry
{
    [JsonPropertyName("mc_path")]
    public required string McPath { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("frame")]
    public string? Frame { get; set; }

    [JsonPropertyName("icon_id")]
    public string? IconId { get; set; }

    [JsonPropertyName("player_head_data")]
    public PlayerHeadDocEntry? PlayerHeadData { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }
}