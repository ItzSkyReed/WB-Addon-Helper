using BacapGenerator.Advancements.Models;
using BacapGenerator.Utils;
using Core.DataComponents.Components;
using Core.DataComponents.Interfaces;
using Core.Items;
using Core.SNBT.Interfaces;
using Core.SNBT.Nodes;
using Core.TextComponents.Components;
using Core.TextComponents.Models;
using JetBrains.Annotations;

namespace BacapGenerator.Advancements.Functions.Trophy;

/// <summary>
/// Wrapper for a Trophy reward item stack.
/// Directly synchronizes data with underlying <see cref="ItemStack"/> components to prevent state drift.
/// </summary>
/// <param name="Item">The underlying item stack containing components and metadata.</param>
/// <param name="DeliveryType">The delivery mechanism used to grant the trophy.</param>
public record TrophyReward(ItemStack Item, TrophyDeliveryType DeliveryType = TrophyDeliveryType.Inventory)
{
    private const string AwardedForAchievingText = "Awarded for achieving";

    /// <summary>
    /// Gets the translation key directly from the item's CustomName component.
    /// </summary>
    public string? Title
    {
        get
        {
            if (Item.Components.TryGet<CustomNameComponent>(out var customName))
            {
                return customName.Value switch
                {
                    TranslatableComponent tc => tc.Translate,
                    PlainTextComponent ptc => ptc.Text,
                    _ => null
                };
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the title color directly from the item's CustomName component.
    /// </summary>
    public string? TitleColor
    {
        get
        {
            if (Item.Components.TryGet<CustomNameComponent>(out var customName) &&
                customName.Value.Style?.Color != null)
                return customName.Value.Style.Color;

            return null;
        }
    }

    /// <summary>
    /// Gets the sanitized description lines from the item's Lore component,
    /// excluding the spacer and 'Awarded for achieving' footer.
    /// </summary>
    public IReadOnlyList<string> DescriptionLines => ParseLore().Description;

    /// <summary>
    /// Gets the translation key or display title of the advancement that awards this trophy.
    /// </summary>
    public string? AwardedAdvancementTitle => ParseLore().AwardedTitle;

    /// <summary>
    /// Gets the color associated with the awarded advancement title line in the lore footer.
    /// </summary>
    public string? AwardedAdvancementColor => ParseLore().AwardedColor;

    /// <summary>
    /// Sets or updates the trophy Lore component conforming to the standard BACAP template
    /// directly from a parent <see cref="BacapAdvancement"/> model.
    /// </summary>
    /// <param name="descriptionLines">The optional custom description lines.</param>
    /// <param name="advancement">The parent advancement awarding this trophy.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="advancement"/> is null.</exception>
    /// <example>
    /// <code>
    /// trophy.SetStandardLore(["Shows more information about advancement", "requirements"], advancement);
    /// </code>
    /// </example>
    public void SetStandardLore(IEnumerable<string>? descriptionLines, BacapAdvancement advancement)
    {
        ArgumentNullException.ThrowIfNull(advancement);

        SetStandardLore(
            descriptionLines,
            advancementTitle: advancement.TitleText,
            advancementColor: advancement.Tier.MinecraftColor,
            descriptionColor: TitleColor);
    }

    /// <summary>
    /// Sets or updates the trophy Lore component conforming to the standard BACAP template:
    /// custom description lines, an empty spacer, and the 'Awarded for achieving' footer.
    /// </summary>
    /// <param name="descriptionLines">The optional custom description lines.</param>
    /// <param name="advancementTitle">The name or translation key of the awarding advancement.</param>
    /// <param name="advancementColor">The color of the awarded advancement (typically the tier color).</param>
    /// <param name="descriptionColor">Optional custom color for description lines. Defaults to <see cref="TitleColor"/> or "gray".</param>
    /// <example>
    /// <code>
    /// trophy.SetStandardLore(
    ///     descriptionLines: ["Shows more information about advancement", "requirements"],
    ///     advancementTitle: "Advancement Info",
    ///     advancementColor: "#FFAEFF",
    ///     descriptionColor: "#B0CCD8");
    /// </code>
    /// </example>
    public void SetStandardLore(
        IEnumerable<string>? descriptionLines,
        string? advancementTitle,
        string? advancementColor,
        string? descriptionColor = null)
    {
        var lines = new List<TextComponent>();
        var descColor = descriptionColor ?? TitleColor ?? "gray";

        if (descriptionLines is not null)
        {
            foreach (var descLine in descriptionLines)
            {
                if (string.IsNullOrWhiteSpace(descLine))
                    continue;

                lines.Add(new TranslatableComponent(descLine.Trim(), Style: new TextStyle(Color: descColor)));
            }
        }

        if (!string.IsNullOrWhiteSpace(advancementTitle))
        {
            if (lines.Count > 0)
                lines.Add(new PlainTextComponent(" "));

            lines.Add(new TranslatableComponent(AwardedForAchievingText, Style: new TextStyle(Color: "gray")));
            lines.Add(new TranslatableComponent(advancementTitle, Style: new TextStyle(Color: advancementColor ?? "white", Italic: false)));
        }

        Item.Components.Set(new LoreComponent(lines));
    }

    /// <summary>
    /// Factory method to construct a trophy item with all necessary components configured according to BACAP standards.
    /// </summary>
    /// <param name="advancement">The parent advancement granting this trophy.</param>
    /// <param name="itemId">The base Minecraft item ID (e.g., "minecraft:cobblestone_stairs").</param>
    /// <param name="titleKey">The translation key for the trophy title.</param>
    /// <param name="titleColor">The hex color string for the title and description.</param>
    /// <param name="count">The item stack count.</param>
    /// <param name="deliveryType">The delivery mechanism for granting the trophy.</param>
    /// <param name="descriptionLines">Optional custom description lines for the lore.</param>
    /// <param name="extraSetComponents">Extra item components to add or overwrite.</param>
    /// <param name="extraRemoveComponents">Component IDs to explicitly remove.</param>
    /// <returns>A new <see cref="TrophyReward"/> wrapper with the fully configured item stack.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="advancement"/> is null.</exception>
    /// <example>
    /// <code>
    /// var trophy = TrophyReward.CreateNew(
    ///     advancement,
    ///     itemId: "minecraft:cobblestone_stairs",
    ///     titleKey: "Advancement's Stairs",
    ///     titleColor: "#B0CCD8",
    ///     descriptionLines: ["Shows more information about advancement", "requirements"]);
    /// </code>
    /// </example>
    public static TrophyReward CreateNew(
        BacapAdvancement advancement,
        string itemId,
        string titleKey,
        string titleColor,
        int count = 1,
        TrophyDeliveryType deliveryType = TrophyDeliveryType.Inventory,
        IEnumerable<string>? descriptionLines = null,
        IEnumerable<IDataComponent>? extraSetComponents = null,
        IEnumerable<string>? extraRemoveComponents = null)
    {
        ArgumentNullException.ThrowIfNull(advancement);

        var item = new ItemStack(itemId, count);

        // Mark as Trophy (custom_data: {Trophy: 1})
        var customDataTags = new Dictionary<string, ISnbtNode>
        {
            ["Trophy"] = new SnbtBool(true)
        };
        item.Components.Set(new CustomDataComponent(new SnbtCompound(customDataTags)));

        // Set Custom Name
        var style = new TextStyle(Color: titleColor, Bold: true, Italic: false);
        var customName = new TranslatableComponent(titleKey, Style: style);
        item.Components.Set(new CustomNameComponent(customName));

        // Set custom_model_data
        var customModelData = new CustomModelDataComponent(
            Strings: [$"{advancement.Datapack.Settings.MainNamespace}:{BacapUtils.ToSnakeCaseSlug(titleKey)}"]);
        item.Components.Set(customModelData);

        var trophy = new TrophyReward(item, deliveryType);
        // Apply extra components first
        if (extraSetComponents is not null)
            foreach (var setComponent in extraSetComponents)
                item.Components.Set(setComponent);

        // Generate Standard BACAP Lore (if caller didn't explicitly supply a custom LoreComponent, or if descriptionLines were provided)
        if (descriptionLines is not null || !item.Components.Has<LoreComponent>())
            trophy.SetStandardLore(descriptionLines, advancement);

        // Process explicitly removed components
        if (extraRemoveComponents is null)
            return trophy;

        foreach (var componentId in extraRemoveComponents)
            item.Components.Remove(componentId);

        return trophy;
    }

    /// <summary>
    /// Updates the trophy item components to match current datapack standards.
    /// Synchronizes custom_model_data, base tags, and re-generates the standard lore footer.
    /// </summary>
    /// <param name="advancement">The parent advancement granting this trophy.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="advancement"/> is null.</exception>
    [PublicAPI]
    public void Standardize(BacapAdvancement advancement)
    {
        ArgumentNullException.ThrowIfNull(advancement);

        // Standardize Custom Model Data
        var titleKey = Title;
        if (!string.IsNullOrWhiteSpace(titleKey))
        {
            var expectedModelId = $"{advancement.Datapack.Settings.MainNamespace}:{BacapUtils.ToSnakeCaseSlug(titleKey)}";
            Item.Components.Set(new CustomModelDataComponent(Strings: [expectedModelId]));
        }

        // Ensure Trophy marker tag is intact
        if (Item.Components.TryGet<CustomDataComponent>(out var customData))
        {
            if (customData.Tag is { } compound && !compound.Tags.ContainsKey("Trophy"))
            {
                var newTags = new Dictionary<string, ISnbtNode>(compound.Tags)
                {
                    ["Trophy"] = new SnbtBool(true)
                };
                Item.Components.Set(new CustomDataComponent(new SnbtCompound(newTags)));
            }
        }
        else
        {
            var customDataTags = new Dictionary<string, ISnbtNode> { ["Trophy"] = new SnbtBool(true) };
            Item.Components.Set(new CustomDataComponent(new SnbtCompound(customDataTags)));
        }

        // Standardize Lore: keep current description lines and re-apply advancement footer
        SetStandardLore(DescriptionLines, advancement);
    }

    /// <summary>
    /// Parses the existing Lore component into structured description lines and footer metadata.
    /// </summary>
    /// <returns>A tuple containing description lines, the awarded advancement title, and its color.</returns>
    private (List<string> Description, string? AwardedTitle, string? AwardedColor) ParseLore()
    {
        if (!Item.Components.TryGet<LoreComponent>(out var loreComponent) || loreComponent.Lines.Count == 0)
            return ([], null, null);

        var awardedIndex = -1;
        for (var i = 0; i < loreComponent.Lines.Count; i++)
        {
            var text = GetLineText(loreComponent.Lines[i]);
            if (!string.Equals(text?.Trim(), AwardedForAchievingText, StringComparison.OrdinalIgnoreCase))
                continue;

            awardedIndex = i;
            break;
        }

        var descriptions = new List<string>();
        var descriptionEnd = awardedIndex >= 0 ? awardedIndex : loreComponent.Lines.Count;

        for (var i = 0; i < descriptionEnd; i++)
        {
            var text = GetLineText(loreComponent.Lines[i]);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            descriptions.Add(text);
        }

        string? awardedTitle = null;
        string? awardedColor = null;

        if (awardedIndex < 0 || awardedIndex + 1 >= loreComponent.Lines.Count)
            return (descriptions, awardedTitle, awardedColor);

        var awardedLine = loreComponent.Lines[awardedIndex + 1];
        awardedTitle = GetLineText(awardedLine);
        awardedColor = awardedLine.Style?.Color;

        return (descriptions, awardedTitle, awardedColor);
    }

    private static string? GetLineText(TextComponent line) => line switch
    {
        TranslatableComponent tc => tc.Translate,
        PlainTextComponent ptc => ptc.Text,
        _ => null
    };
}