using BacapGenerator.Advancements.Models;
using Core.Commands.Impl;
using Core.Commands.Models;
using Core.McFunctions.Models;
using Core.Registries;
using Core.TextComponents.Components;
using Core.TextComponents.Models;
using JetBrains.Annotations;

namespace BacapGenerator.Advancements.Functions;

/// <summary>
/// Represents the experience reward function file.
/// Manages both the /experience grant command and its corresponding /tellraw announcement,
/// while preserving any custom commands in the file.
/// </summary>
public sealed class ExpRewardFunction : BaseFunction
{
    private int _experienceAmount;

    /// <summary>
    /// Gets or sets the amount of experience to reward.
    /// Setting this property automatically updates the underlying function file lines.
    /// </summary>
    [PublicAPI]
    public int ExperienceAmount
    {
        get => _experienceAmount;
        set
        {
            if (_experienceAmount == value)
                return;

            _experienceAmount = value;
            Update();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpRewardFunction"/> class
    /// and attempts to parse the existing experience amount from the memory AST.
    /// </summary>
    /// <param name="file">The physical file information.</param>
    /// <param name="parsedFunction">The parsed McFunction AST data.</param>
    /// <param name="bacapAdvancement">The BACAP advancement model associated with this function.</param>
    internal ExpRewardFunction(FileInfo file, McFunction parsedFunction, BacapAdvancement bacapAdvancement)
        : base(file, parsedFunction, bacapAdvancement)
    {
        ParseExistingExperience();
    }

    /// <summary>
    /// Generates or updates the experience reward and tellraw commands.
    /// Identifies the existing commands by looking for standard heuristics
    /// to preserve other custom commands in the file.
    /// </summary>
    /// <summary>
    /// Updates or removes the experience reward and tellraw commands depending on whether the amount is greater than zero.
    /// </summary>
    [PublicAPI]
    public override void Update()
    {
        var xpIndex = Function.Lines.FindIndex(line =>
            line is ExecutableLine { Command: ExperienceCommand { Action: ExperienceAction.Add } expCmd } &&
            expCmd.Target == Selector.SelectedPlayer);

        var tellrawIndex = Function.Lines.FindIndex(line =>
            line is ExecutableLine { Command: TellrawCommand tellCmd } &&
            tellCmd.Target == Selector.SelectedPlayer &&
            (tellCmd.Message.Extra?.Any(extraComp =>
                extraComp is TranslatableComponent { Translate: "Experience" }) ?? false));

        // If no experience reward is granted, remove any previously generated reward lines.
        if (_experienceAmount <= 0)
        {
            // Remove the higher index first to prevent index shifting issues.
            var indicesToRemove = new[] { xpIndex, tellrawIndex }
                .Where(i => i >= 0)
                .OrderByDescending(i => i);

            foreach (var index in indicesToRemove)
                Function.Lines.RemoveAt(index);

            return;
        }

        // Otherwise, generate the reward commands.
        var xpLine = new ExecutableLine(new ExperienceCommand(ExperienceAction.Add, Selector.SelectedPlayer, _experienceAmount));
        var tellrawLine = new ExecutableLine(CreateExperienceMessage(_experienceAmount));

        // Replace or insert the experience command.
        if (xpIndex >= 0)
            Function.Lines[xpIndex] = xpLine;
        else
        {
            Function.Lines.Insert(0, xpLine);
            xpIndex = 0;
        }

        // Re-evaluate tellraw index if the XP line insertion shifted elements.
        tellrawIndex = Function.Lines.FindIndex(line =>
            line is ExecutableLine { Command: TellrawCommand tellCmd } &&
            tellCmd.Target == Selector.SelectedPlayer &&
            (tellCmd.Message.Extra?.Any(extraComp =>
                extraComp is TranslatableComponent { Translate: "Experience" }) ?? false));

        // Replace or insert the tellraw command.
        if (tellrawIndex >= 0)
        {
            Function.Lines[tellrawIndex] = tellrawLine;
        }
        else
        {
            Function.Lines.Insert(xpIndex + 1, tellrawLine);
        }
    }

    /// <summary>
    /// Creates the specific tellraw command for the experience reward announcement.
    /// </summary>
    /// <example>
    /// tellraw @s {"color":"blue","text":" +[exp],"extra":[{"translate":"Experience"}]}
    /// </example>
    private static TellrawCommand CreateExperienceMessage(int amount)
    {
        var rootMessage = new PlainTextComponent(
            Text: $" +{amount} ",
            Style: new TextStyle(Color: "blue"),
            Extra:
            [
                new TranslatableComponent("Experience")
            ]
        );

        return new TellrawCommand(Target: Selector.SelectedPlayer, Message: rootMessage);
    }

    /// <summary>
    /// Scans the currently loaded function lines to extract the existing experience amount
    /// </summary>
    private void ParseExistingExperience()
    {
        foreach (var line in Function.Lines)
        {
            if (line is not ExecutableLine { Command: ExperienceCommand { Action: ExperienceAction.Add } expCmd } ||
                expCmd.Target != Selector.SelectedPlayer ||
                !expCmd.Amount.HasValue) continue;
            _experienceAmount = expCmd.Amount.Value;
            return;
        }

        _experienceAmount = 0;
    }
}