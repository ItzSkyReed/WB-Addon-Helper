using BacapGenerator.Advancements.Functions;
using BacapGenerator.Advancements.Functions.Trophy;
using BacapGenerator.Advancements.Models;
using BacapGenerator.Datapacks.Models;
using BacapGenerator.Utils;
using Core.Advancements.Models;
using Core.McFunctions.Models;
using Core.Registries;

namespace BacapGenerator.Advancements;

/// <summary>
/// Factory responsible for reading files, parsing JSON/McFunction ASTs,
/// and instantiating the appropriate <see cref="ManagedAdvancement"/> subtype.
/// </summary>
public static class AdvancementFactory
{
    /// <summary>
    /// Creates a <see cref="ManagedAdvancement"/> from a raw JSON string content.
    /// </summary>
    /// <param name="file">The physical advancement file info.</param>
    /// <param name="jsonContent">The raw JSON string to parse.</param>
    /// <param name="datapack">The owning datapack.</param>
    /// <param name="minecraftData">Minecraft global registries</param>
    /// <returns>
    /// An instance of <see cref="ValidAdvancement"/> (<see cref="BacapAdvancement"/> or <see cref="TechnicalAdvancement"/>)
    /// if successfully parsed; otherwise, an <see cref="InvalidAdvancement"/>.
    /// </returns>
    public static ManagedAdvancement Create(FileInfo file, string jsonContent, IReadOnlyDatapack datapack)
    {
        try
        {
            var parsedData = Advancement.Parse(jsonContent);
            if (parsedData is null)
            {
                return new InvalidAdvancement(file, null, datapack, AdvancementParsingError.MalformedJson);
            }

            return Create(file, parsedData, datapack);
        }
        catch
        {
            return new InvalidAdvancement(file, null, datapack, AdvancementParsingError.MalformedJson);
        }
    }

    /// <summary>
    /// Creates a <see cref="ManagedAdvancement"/> from an already parsed <see cref="Advancement"/> model.
    /// </summary>
    /// <param name="file">The physical advancement file info.</param>
    /// <param name="parsedData">The valid, parsed core advancement model.</param>
    /// <param name="datapack">The owning datapack.</param>
    /// <param name="minecraftData">Minecraft global registries</param>
    /// <returns>
    /// A <see cref="TechnicalAdvancement"/> if display metadata is missing,
    /// a fully initialized <see cref="BacapAdvancement"/> if valid,
    /// or an <see cref="InvalidAdvancement"/> describing the failure reason.
    /// </returns>
    public static ManagedAdvancement Create(FileInfo file, Advancement parsedData, IReadOnlyDatapack datapack)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(parsedData);
        ArgumentNullException.ThrowIfNull(datapack);

        // Technical advancements do not have display elements or associated functions
        if (parsedData.Display is null)
            return new TechnicalAdvancement(file, parsedData, datapack);

        if (!BacapAdvancement.TryCreate(file, parsedData, datapack, out var bacapAdv, out var error))
            return new InvalidAdvancement(file, parsedData, datapack, error);

        try
        {
            // bacapAdv is recognized as non-null by the compiler via [NotNullWhen(true)]
            LoadFunctions(bacapAdv);
            return bacapAdv;
        }
        catch
        {
            return new InvalidAdvancement(file, parsedData, datapack, AdvancementParsingError.FailedToLoadAssociatedFunctions);
        }
    }

    /// <summary>
    /// Locates, reads, and parses the five function files associated with a BACAP advancement,
    /// injecting them directly into the model instance.
    /// </summary>
    /// <param name="adv">The target BACAP advancement instance.</param>
    private static void LoadFunctions(BacapAdvancement adv)
    {
        var relativePath = $"{MinecraftUtils.StripNamespace(adv.Advancement.Rewards!.Function!)}.mcfunction";
        var basePath = Path.Combine(adv.Datapack.DatapackDataPath.FullName, adv.Datapack.Settings.RewardNamespace, "function");

        var macroFile = new FileInfo(Path.Combine(basePath, relativePath));
        var msgFile = new FileInfo(Path.Combine(basePath, "msg", relativePath));
        var expFile = new FileInfo(Path.Combine(basePath, "exp", relativePath));
        var itemFile = new FileInfo(Path.Combine(basePath, "reward", relativePath));
        var trophyFile = new FileInfo(Path.Combine(basePath, "trophy", relativePath));

        adv.MacroFunction = new MacroFunction(macroFile, ParseMcFunction(macroFile), adv);
        adv.MsgFunction = new MsgFunction(msgFile, ParseMcFunction(msgFile), adv);
        adv.ExpRewardFunction = new ExpRewardFunction(expFile, ParseMcFunction(expFile), adv);
        adv.ItemRewardFunction = new ItemRewardFunction(itemFile, ParseMcFunction(itemFile), adv);
        adv.TrophyRewardFunction = new TrophyRewardFunction(trophyFile, ParseMcFunction(trophyFile), adv);
    }

    /// <summary>
    /// Reads the physical mcfunction file if it exists and parses it into an AST.
    /// Returns an empty AST if the file does not exist on disk.
    /// </summary>
    /// <param name="file">The physical function file info.</param>
    /// <returns>The parsed <see cref="McFunction"/> AST.</returns>
    private static McFunction ParseMcFunction(FileInfo file)
    {
        file.Refresh();
        var content = file.Exists ? File.ReadAllText(file.FullName) : string.Empty;
        return McFunction.Parse(content);
    }
}