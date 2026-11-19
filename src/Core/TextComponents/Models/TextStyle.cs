using Core.SNBT;

using Core.TextComponents.Events;
using JetBrains.Annotations;

namespace Core.TextComponents.Models;

public record TextStyle(
    string? Color = null,
    string? Font = null,
    bool? Bold = null,
    bool? Italic = null,
    bool? Underlined = null,
    bool? Strikethrough = null,
    bool? Obfuscated = null,
    string? Insertion = null,
    ClickEvent? ClickEvent = null,
    HoverEvent? HoverEvent = null
)
{
    [PublicAPI]
    public static TextStyle Empty { get; } = new();

    public void ApplyTo(SnbtCompoundBuilder builder)
    {
        builder
            .PutOptional("color", Color)
            .PutOptional("font", Font)
            .PutOptional("bold", Bold)
            .PutOptional("italic", Italic)
            .PutOptional("underlined", Underlined)
            .PutOptional("strikethrough", Strikethrough)
            .PutOptional("obfuscated", Obfuscated)
            .PutOptional("insertion", Insertion);


        if (ClickEvent != null)
            builder.Put("click_event", ClickEvent.ToSnbt());
        if (HoverEvent != null)
            builder.Put("hover_event", HoverEvent.ToSnbt());
    }
}