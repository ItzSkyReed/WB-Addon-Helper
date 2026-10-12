using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using BacapGenerator.Advancements.Models;
using BacapGenerator.Datapacks.Models.Settings;
using Core.Registries;

namespace BacapGenerator.Datapacks.Models;

/// <summary>
/// Represents a loaded Minecraft Datapack, containing its settings, global registry data,
/// and all parsed advancements.
/// </summary>
public class Datapack : IReadOnlyDatapack
{
    /// <summary>
    /// Gets the strongly-typed identifier of the datapack.
    /// </summary>
    /// <summary>
    /// Gets the string identifier of the datapack, loaded from the configuration.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the settings and configuration associated with this datapack.
    /// </summary>
    public DatapackSettings Settings { get; init; }

    /// <summary>
    /// Gets the <see cref="DirectoryInfo"/> representing the data folder of the datapack.
    /// </summary>
    public DirectoryInfo DatapackDataPath { get; init; }

    /// <summary>
    /// Gets the global Minecraft registry data.
    /// </summary>
    public MinecraftData MinecraftData { get; init; }

    /// <summary>
    /// Gets the mutable collection of advancements belonging to this datapack.
    /// </summary>
    public List<ManagedAdvancement> Advancements { get; } = [];

    /// <inheritdoc cref="IReadOnlyDatapack.Advancements"/>
    IReadOnlyList<ManagedAdvancement> IReadOnlyDatapack.Advancements => Advancements.AsReadOnly();


    /// <summary>
    /// Gets a cached lookup mapping advancement McPath to its model.
    /// Safely handles duplicate paths and ignores casing.
    /// </summary>
    public IReadOnlyDictionary<string, ManagedAdvancement> AdvancementsById
    {
        get => field ??= BuildLookup();
        private set;
    }

    /// <summary>
    /// Attempts to find an advancement by its mcPath.
    /// </summary>
    /// <param name="mcPath">The advancement resource path to search for.</param>
    /// <param name="advancement">When found, contains the matching advancement; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetAdvancement(string mcPath, [NotNullWhen(true)] out ManagedAdvancement? advancement)
    {
        if (!string.IsNullOrWhiteSpace(mcPath))
            return AdvancementsById.TryGetValue(mcPath, out advancement);

        advancement = null;
        return false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Datapack"/> model.
    /// Does not perform file I/O operations.
    /// </summary>
    /// <param name="id">The strongly-typed identifier of the datapack.</param>
    /// <param name="settings">The datapack configuration.</param>
    /// <param name="minecraftData">The global Minecraft registry data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="minecraftData"/> is null.</exception>
    public Datapack(string id, DatapackSettings settings, MinecraftData minecraftData)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(minecraftData);

        Id = id;
        Settings = settings;
        DatapackDataPath = new DirectoryInfo(Path.Combine(Settings.Path, "data"));
    }


    /// <summary>
    /// Populates advancements and resets the lookup cache.
    /// </summary>
    /// <param name="parsedAdvancements">The collection of loaded advancements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parsedAdvancements"/> is null.</exception>
    internal void InitializeAdvancements(IEnumerable<ManagedAdvancement> parsedAdvancements)
    {
        ArgumentNullException.ThrowIfNull(parsedAdvancements);
        Advancements.AddRange(parsedAdvancements);
        AdvancementsById = null!; // Reset cache
    }

    /// <summary>
    /// Compiles current advancements into an immutable dictionary, skipping empty or duplicate paths.
    /// </summary>
    /// <returns>A case-insensitive frozen dictionary mapping McPath to advancement.</returns>
    private FrozenDictionary<string, ManagedAdvancement> BuildLookup()
    {
        var map = new Dictionary<string, ManagedAdvancement>(Advancements.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var adv in Advancements)
            map.TryAdd(adv.McPath, adv);

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}