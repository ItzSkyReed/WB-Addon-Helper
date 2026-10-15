using System.Diagnostics.CodeAnalysis;
using BacapGenerator.Advancements.Models;
using Core.Commands.Impl;
using Core.Commands.Models;
using Core.DataComponents.Components;
using Core.Items;
using Core.McFunctions.Models;
using Core.McFunctions.Models.Interfaces;
using Core.SNBT;
using Core.SNBT.Nodes;
using Core.TextComponents.Components;
using Core.TextComponents.Models;
using JetBrains.Annotations;

namespace BacapGenerator.Advancements.Functions.Trophy;

/// <summary>
/// Represents the trophy reward function file.
/// Manages trophy item rewards and their announcements (tellraw @s).
/// </summary>
public sealed class TrophyRewardFunction : BaseFunction
{
    private const string DeathLocationMessageText = " The trophy appeared at the place of your death";

    private readonly List<TrophyReward> _trophies = [];

    [PublicAPI]
    public IReadOnlyList<TrophyReward> Trophies => _trophies;

    /// <summary>
    /// Replaces the current collection of trophies and updates the function commands.
    /// </summary>
    /// <param name="trophies">The updated collection of trophies.</param>
    [PublicAPI]
    public void SetTrophies(IEnumerable<TrophyReward> trophies)
    {
        ArgumentNullException.ThrowIfNull(trophies);

        _trophies.Clear();
        _trophies.AddRange(trophies);
        Update();
    }

    /// <summary>
    /// Clears all trophy rewards, generating an empty function file.
    /// </summary>
    [PublicAPI]
    public void ClearTrophies()
    {
        _trophies.Clear();
        Update();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrophyRewardFunction"/> class.
    /// </summary>
    /// <param name="file">The physical file information.</param>
    /// <param name="parsedFunction">The parsed McFunction AST data.</param>
    /// <param name="bacapAdvancement">The BACAP advancement model associated with this function.</param>
    internal TrophyRewardFunction(FileInfo file, McFunction parsedFunction, BacapAdvancement bacapAdvancement)
        : base(file, parsedFunction, bacapAdvancement)
    {
        ParseExistingTrophies();
    }

    /// <summary>
    /// Adds a new trophy reward to the function if it doesn't already exist.
    /// Duplicates (matching delivery type, item ID, count, and components) are silently ignored.
    /// </summary>
    /// <param name="trophy">The trophy reward to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="trophy"/> is null.</exception>
    [PublicAPI]
    public void AddTrophy(TrophyReward trophy)
    {
        ArgumentNullException.ThrowIfNull(trophy);

        if (_trophies.Any(t => IsDuplicate(t, trophy)))
            return;

        _trophies.Add(trophy);
        Update();
    }

    /// <summary>
    /// Adds a collection of trophy rewards to the function, ignoring duplicates.
    /// </summary>
    /// <param name="trophies">The collection of trophies to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="trophies"/> is null.</exception>
    [PublicAPI]
    public void AddTrophies(IEnumerable<TrophyReward> trophies)
    {
        ArgumentNullException.ThrowIfNull(trophies);

        var hasChanges = false;

        foreach (var trophy in trophies)
        {
            if (_trophies.Any(t => IsDuplicate(t, trophy)))
                continue;

            _trophies.Add(trophy);
            hasChanges = true;
        }

        if (hasChanges)
            Update();
    }

    /// <summary>
    /// Synchronizes the in-memory trophy list with the underlying <see cref="McFunction"/> AST lines,
    /// replacing old trophy commands and announcements while preserving custom commands.
    /// </summary>
    [PublicAPI]
    public override void Update()
    {
        var linesToRemove = new List<IMcFunctionLine>();
        var insertIndex = -1;

        for (var i = 0; i < Function.Lines.Count; i++)
        {
            var line = Function.Lines[i];
            var isTrophyBlockLine = line switch
            {
                ExecutableLine { Command: GiveCommand giveCmd }
                    when giveCmd.Target == Selector.SelectedPlayer && IsTrophyItem(giveCmd.Item) => true,

                ExecutableLine { Command: SummonCommand summonCmd }
                    when TryExtractTrophyFromSummon(summonCmd, out _) => true,

                ExecutableLine { Command: TellrawCommand tellCmd }
                    when tellCmd.Target == Selector.SelectedPlayer && IsGeneratedTrophyTellraw(tellCmd.Message) => true,

                _ => false
            };

            if (!isTrophyBlockLine)
                continue;

            linesToRemove.Add(line);

            if (insertIndex == -1)
                insertIndex = i;
        }

        foreach (var line in linesToRemove)
            Function.Lines.Remove(line);

        foreach (var trophy in _trophies)
        {
            trophy.Standardize(BacapAdvancement);
        }

        var newLines = new List<IMcFunctionLine>();
        foreach (var trophy in _trophies)
        {
            var tellrawCmd = CreateTrophyMessage(trophy);

            switch (trophy.DeliveryType)
            {
                case TrophyDeliveryType.Inventory:
                {
                    var giveCmd = new GiveCommand(Selector.SelectedPlayer, trophy.Item);
                    newLines.Add(new ExecutableLine(giveCmd));
                    newLines.Add(new ExecutableLine(tellrawCmd));
                    break;
                }

                case TrophyDeliveryType.DeathLocation:
                {
                    var summonCmd = CreateSummonItemCommand(trophy.Item);
                    var deathMessageCmd = CreateDeathLocationMessage();

                    newLines.Add(new ExecutableLine(summonCmd));
                    newLines.Add(new ExecutableLine(tellrawCmd));
                    newLines.Add(new ExecutableLine(deathMessageCmd));
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(trophy.DeliveryType), trophy.DeliveryType, null);
            }
        }

        if (insertIndex == -1)
            Function.Lines.AddRange(newLines);
        else
            Function.Lines.InsertRange(insertIndex, newLines);
    }

    private void ParseExistingTrophies()
    {
        var parsedTrophies = new List<TrophyReward>();

        foreach (var line in Function.Lines)
        {
            if (line is not ExecutableLine execLine)
                continue;

            switch (execLine.Command)
            {
                case GiveCommand giveCmd when
                    giveCmd.Target == Selector.SelectedPlayer &&
                    IsTrophyItem(giveCmd.Item):
                {
                    var amount = giveCmd.Count ?? giveCmd.Item.Count;
                    var itemStack = giveCmd.Item with { Count = amount };
                    parsedTrophies.Add(new TrophyReward(itemStack));
                    break;
                }
                case SummonCommand summonCmd when
                    TryExtractTrophyFromSummon(summonCmd, out var itemStack):
                    parsedTrophies.Add(new TrophyReward(itemStack, TrophyDeliveryType.DeathLocation));
                    break;
            }
        }

        _trophies.Clear();
        _trophies.AddRange(parsedTrophies);
    }

    /// <summary>
    /// Strictly verifies whether a <see cref="TextComponent"/> matches either the trophy grant announcement
    /// or the death location notice generated by this class.
    /// </summary>
    /// <param name="component">The root message component from a tellraw command.</param>
    /// <returns><see langword="true"/> if the component is an auto-generated trophy message; otherwise, <see langword="false"/>.</returns>
    private static bool IsGeneratedTrophyTellraw(TextComponent? component)
    {
        return component switch
        {
            // Case 1: Standard trophy announcement: {"color":"gold","text":" +<int> ","extra":[{"translate":"..."}]}
            PlainTextComponent
            {
                Style.Color: "gold",
                Text: var text,
                Extra: [TranslatableComponent { Translate.Length: > 0 }]
            } => IsPlusAmountPrefix(text),

            // Case 2: Death location notice (created as TranslatableComponent in CreateDeathLocationMessage)
            TranslatableComponent
            {
                Style.Color: "gray",
                Translate: var translate
            } => translate.AsSpan().Trim().Equals(DeathLocationMessageText.AsSpan().Trim(), StringComparison.OrdinalIgnoreCase),

            // Case 3: Legacy fallback if death location notice was written as PlainTextComponent
            PlainTextComponent
            {
                Style.Color: "gray",
                Text: var deathText
            } => deathText.AsSpan().Trim().Equals(DeathLocationMessageText.AsSpan().Trim(), StringComparison.OrdinalIgnoreCase),

            _ => false
        };
    }

    /// <summary>
    /// Checks if the string matches the exact <c>" +&lt;digits&gt; "</c> format (e.g., <c>" +1 "</c>).
    /// </summary>
    private static bool IsPlusAmountPrefix(ReadOnlySpan<char> text)
    {
        if (!text.StartsWith(" +") || !text.EndsWith(" "))
            return false;

        var numberSlice = text[2..^1];
        return !numberSlice.IsEmpty && int.TryParse(numberSlice, out var count) && count > 0;
    }

    /// <summary>
    /// Checks whether two trophy rewards represent the same reward.
    /// </summary>
    private static bool IsDuplicate(TrophyReward existing, TrophyReward candidate)
    {
        return existing.DeliveryType == candidate.DeliveryType &&
               existing.Item.Id == candidate.Item.Id &&
               existing.Item.Count == candidate.Item.Count &&
               existing.Item.Components.ToSnbtString() == candidate.Item.Components.ToSnbtString();
    }

    /// <summary>
    /// Checks if the item components contain the Trophy custom_data marker.
    /// </summary>
    private static bool IsTrophyItem(ItemStack item)
    {
        return item.Components.TryGet<CustomDataComponent>(out var customData)
               && customData.Tag.Tags.ContainsKey("Trophy");
    }

    /// <summary>
    /// Attempts to extract a trophy item stack from a summon command.
    /// </summary>
    /// <param name="summonCmd">The summon command AST model.</param>
    /// <param name="trophyItem">The extracted trophy item stack if successful.</param>
    /// <returns><see langword="true"/> if the summon command spawned a trophy item; otherwise, <see langword="false"/>.</returns>
    private static bool TryExtractTrophyFromSummon(
        SummonCommand summonCmd,
        [NotNullWhen(true)] out ItemStack? trophyItem)
    {
        trophyItem = null;

        if (summonCmd.EntityId is not ("minecraft:item" or "item") || summonCmd.Nbt is null)
            return false;

        if (summonCmd.Nbt.GetNode("Item") is not SnbtCompound itemCompound)
            return false;

        var item = ItemStack.Parse(itemCompound);
        if (!IsTrophyItem(item))
            return false;

        trophyItem = item;
        return true;
    }

    /// <summary>
    /// Builds the <see cref="SummonCommand"/> to spawn the trophy item at the current location.
    /// </summary>
    /// <param name="item">The trophy item stack to spawn.</param>
    /// <returns>A configured <see cref="SummonCommand"/> instance.</returns>
    private static SummonCommand CreateSummonItemCommand(ItemStack item)
    {
        var itemBuilder = Snbt.Compound()
            .Put("id", item.Id)
            .Put("count", item.Count);

        if (!item.Components.IsEmpty)
            itemBuilder.Put("components", item.Components.ToSnbt());

        var entityNbt = Snbt.Compound()
            .Put("Invulnerable", new SnbtBool(true))
            .Put("Item", itemBuilder.Build())
            .Build();

        return new SummonCommand("minecraft:item", new Position("~", "~", "~"), entityNbt);
    }

    /// <summary>
    /// Creates the tellraw message notifying the player of the trophy reward.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the trophy item lacks a custom name or translation key.</exception>
    private static TellrawCommand CreateTrophyMessage(TrophyReward trophy)
    {
        var title = trophy.Title
                    ?? throw new InvalidOperationException(
                        $"Cannot create tellraw message: Trophy item '{trophy.Item.Id}' is missing a custom name or translation key.");

        var rootMessage = new PlainTextComponent(
            Text: $" +{trophy.Item.Count} ",
            Style: new TextStyle(Color: "gold"),
            Extra:
            [
                new TranslatableComponent(title)
            ]
        );

        return new TellrawCommand(Target: Selector.SelectedPlayer, Message: rootMessage);
    }

    /// <summary>
    /// Creates the tellraw message informing the player that the trophy appeared at their death location.
    /// </summary>
    /// <returns>A configured <see cref="TellrawCommand"/> instance.</returns>
    private static TellrawCommand CreateDeathLocationMessage()
    {
        var message = new TranslatableComponent(
            Translate: DeathLocationMessageText,
            Style: new TextStyle(Color: "gray")
        );

        return new TellrawCommand(Target: Selector.SelectedPlayer, Message: message);
    }
}