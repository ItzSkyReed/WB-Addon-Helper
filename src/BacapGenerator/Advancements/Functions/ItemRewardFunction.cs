using BacapGenerator.Advancements.Models;
using BacapGenerator.Utils;
using Core.Commands.Impl;
using Core.Commands.Models;
using Core.Items;
using Core.McFunctions.Models;
using Core.McFunctions.Models.Interfaces;
using Core.TextComponents.Components;
using Core.TextComponents.Models;
using JetBrains.Annotations;

namespace BacapGenerator.Advancements.Functions;

/// <summary>
/// Represents the item reward function file.
/// Manages multiple item rewards (give) and their personal local announcements (tellraw @s),
/// while preserving any custom commands in the file.
/// </summary>
public sealed class ItemRewardFunction : BaseFunction
{
    /// <summary>
    /// Gets the list of items to reward the player.
    /// Setting this property automatically updates the underlying function file lines.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when an item id is not found in the registry to get its translation key.</exception>
    [PublicAPI]
    public IReadOnlyList<ItemStack> RewardItems { get; private set; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemRewardFunction"/> class.
    /// </summary>
    /// <param name="file">The physical file information.</param>
    /// <param name="parsedFunction">The parsed McFunction AST data.</param>
    /// <param name="bacapAdvancement">The BACAP advancement model associated with this function.</param>
    internal ItemRewardFunction(FileInfo file, McFunction parsedFunction, BacapAdvancement bacapAdvancement)
        : base(file, parsedFunction, bacapAdvancement)
    {
        ParseExistingItems();
    }

    /// <summary>
    /// Sets the reward items and updates the underlying function commands.
    /// </summary>
    /// <param name="items">The new collection of reward items.</param>
    public void SetRewardItems(IEnumerable<ItemStack> items)
    {
        RewardItems = items.ToList();
        Update();
    }

    /// <summary>
    /// Clears all reward items, effectively removing give and tellraw commands.
    /// </summary>
    public void ClearRewardItems()
    {
        RewardItems = [];
        Update();
    }
/// <summary>
    /// Generates or updates the item reward commands (/give @s) and local announcements (/tellraw @s).
    /// Preserves any unrelated custom commands (like comments or particles) in the file.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when an item id is not found in the registry to get its translation key.</exception>
    [PublicAPI]
    public override void Update()
    {
        var linesToRemove = new List<IMcFunctionLine>();
        var insertIndex = -1;

        // Identify the existing item reward block via strict AST pattern matching
        for (var i = 0; i < Function.Lines.Count; i++)
        {
            var line = Function.Lines[i];
            var isRewardBlockLine = line switch
            {
                ExecutableLine { Command: GiveCommand giveCmd }
                    when giveCmd.Target == Selector.SelectedPlayer => true,

                ExecutableLine { Command: TellrawCommand tellCmd }
                    when tellCmd.Target == Selector.SelectedPlayer && IsGeneratedItemMessage(tellCmd.Message) => true,

                _ => false
            };

            if (!isRewardBlockLine)
                continue;

            linesToRemove.Add(line);

            if (insertIndex == -1)
                insertIndex = i;
        }

        // Clean up old reward lines
        foreach (var line in linesToRemove)
            Function.Lines.Remove(line);

        // Generate new lines for the updated items
        var newLines = new List<IMcFunctionLine>(RewardItems.Count * 2);
        foreach (var item in RewardItems)
        {
            var giveCmd = new GiveCommand(Selector.SelectedPlayer, item);
            var tellrawCmd = CreateItemMessage(item);

            newLines.Add(new ExecutableLine(giveCmd));
            newLines.Add(new ExecutableLine(tellrawCmd));
        }

        // Insert the new lines back into the AST
        if (insertIndex == -1)
            Function.Lines.AddRange(newLines);
        else
            Function.Lines.InsertRange(insertIndex, newLines);
    }

    /// <summary>
    /// Scans the currently loaded function lines to extract existing item rewards.
    /// Automatically sums up amounts for consecutive identical items (e.g., 3x 64 rockets -> 192).
    /// </summary>
    private void ParseExistingItems()
    {
        var parsedItems = new List<ItemStack>();

        foreach (var line in Function.Lines)
        {
            if (line is not ExecutableLine { Command: GiveCommand giveCmd } || giveCmd.Target != Selector.SelectedPlayer)
                continue;

            // Fallback to item.Count if Count was omitted in the give command
            var amount = giveCmd.Count ?? giveCmd.Item.Count;
            var lastItem = parsedItems.LastOrDefault();

            // If it's the exact same item ID and NBT/Components, sum the count
            if (lastItem != null &&
                lastItem.Id == giveCmd.Item.Id &&
                lastItem.Components.ToSnbt().ToString() == giveCmd.Item.Components.ToSnbt().ToString())
                parsedItems[^1] = lastItem with { Count = lastItem.Count + amount };
            else
                // Add as a new item
                parsedItems.Add(giveCmd.Item with { Count = amount });
        }

        RewardItems = parsedItems;
    }

    /// <summary>
    /// Strictly verifies whether a <see cref="TextComponent"/> matches the AST structure
    /// produced by <see cref="CreateItemMessage"/>:
    /// <c>{"color":"green","text":" +&lt;int&gt; ","extra":[{"translate":"..."}]}</c>.
    /// </summary>
    /// <param name="component">The root message component from a tellraw command.</param>
    /// <returns><see langword="true"/> if the component is an auto-generated item reward message; otherwise, <see langword="false"/>.</returns>
    private static bool IsGeneratedItemMessage(TextComponent? component)
    {
        return component is PlainTextComponent
        {
            Style.Color: "green",
            Text: var text,
            Extra: [TranslatableComponent { Translate.Length: > 0 }]
        } && IsPlusAmountPrefix(text);
    }

    /// <summary>
    /// Checks if the string matches the exact <c>" +&lt;digits&gt; "</c> format (e.g., <c>" +16 "</c>).
    /// </summary>
    private static bool IsPlusAmountPrefix(ReadOnlySpan<char> text)
    {
        if (!text.StartsWith(" +") || !text.EndsWith(" "))
            return false;

        var numberSlice = text[2..^1];
        return !numberSlice.IsEmpty && int.TryParse(numberSlice, out var count) && count > 0;
    }

    /// <summary>
    /// Creates the tellraw command for a specific item reward.
    /// </summary>
    /// <param name="item">The item stack to create the message for.</param>
    /// <returns>A tellraw command announcing the item reward.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the item id is not found in the registry.</exception>
    /// <example>
    /// tellraw @s {"color":"green","text":" +16 ","extra":[{"translate":"item.minecraft.egg"}]}
    /// </example>
    private TellrawCommand CreateItemMessage(ItemStack item)
    {
        var itemIdWithoutNamespace = MinecraftUtils.StripNamespace(item.Id);
        if (!BacapAdvancement.Datapack.MinecraftData.Items.TryGetValue(itemIdWithoutNamespace, out var itemRegistryEntry))
            throw new KeyNotFoundException($"Item '{itemIdWithoutNamespace}' not found in Item registry.");

        var rootMessage = new PlainTextComponent(
            Text: $" +{item.Count} ",
            Style: new TextStyle(Color: "green"),
            Extra:
            [
                new TranslatableComponent(itemRegistryEntry.TranslationKey)
            ]
        );

        return new TellrawCommand(Target: Selector.SelectedPlayer, Message: rootMessage);
    }
}