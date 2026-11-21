using System.Text.Json.Serialization;
using BacapGenerator.Converters;
using JetBrains.Annotations;

namespace WbGenerator.DocGen.Documentation.Models;

public class PlayerHeadDocEntry
{
    [JsonPropertyName("uuid")]
    [JsonConverter(typeof(MinecraftGuidJsonConverter))]
    [UsedImplicitly]
    public Guid? Uuid { get; init; }

    [JsonPropertyName("texture_hash")]
    [UsedImplicitly]
    public string? TextureHash { get; init; }
}