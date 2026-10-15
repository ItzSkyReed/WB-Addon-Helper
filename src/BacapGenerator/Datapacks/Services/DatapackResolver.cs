using BacapGenerator.Advancements.Models;
using BacapGenerator.Datapacks.Models;
using JetBrains.Annotations;

namespace BacapGenerator.Datapacks.Services;

/// <summary>
/// Service responsible for resolving inheritance and marking override advancements across related datapacks.
/// </summary>
public static class DatapackResolver
{
    /// <summary>
    /// Compares a child datapack with its parent and marks advancements that override existing parent advancements.
    /// Overridden advancements will have their <see cref="ManagedAdvancement.IsOverride"/> flag set to <see langword="true"/>.
    /// </summary>
    /// <param name="childPack">The child/addon datapack instance (e.g., BacapedHardcore).</param>
    /// <param name="parentPack">The base datapack instance (e.g., Bacaped).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="childPack"/> or <paramref name="parentPack"/> is null.</exception>
    /// <example>
    /// <code>
    /// DatapackResolver.ResolveOverrides(registry.BacapedHardcore, registry.Bacaped);
    /// </code>
    /// </example>
    [PublicAPI]
    public static void ResolveOverrides(Datapack childPack, Datapack parentPack)
    {
        ArgumentNullException.ThrowIfNull(childPack);
        ArgumentNullException.ThrowIfNull(parentPack);

        var parentAdvancementPaths = parentPack.Advancements
            .Select(a => a.McPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var adv in childPack.Advancements)
        {
            if (!parentAdvancementPaths.Contains(adv.McPath))
                continue;

            adv.IsOverride = true;
        }
    }
}