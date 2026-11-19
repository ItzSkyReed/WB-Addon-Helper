using BacapGenerator.Advancements.Models;
using BacapGenerator.Utils;
using Core.Commands.Impl;
using Core.McFunctions.Models;
using Core.McFunctions.Models.Extensions;
using Core.SNBT;
using JetBrains.Annotations;

namespace BacapGenerator.Advancements.Functions;

/// <summary>
/// Represents the main function file that executes the core logic,
/// specifically ensuring that the reward macro command is present.
/// </summary>
public sealed class MacroFunction : BaseFunction
{

    /// <summary>
    /// Initializes a new instance of the <see cref="MacroFunction"/> class.
    /// </summary>
    /// <param name="file">The physical file information.</param>
    /// <param name="parsedFunction">The parsed McFunction AST data.</param>
    /// <param name="bacapAdvancement">The BACAP advancement model associated with this function.</param>
    internal MacroFunction(FileInfo file, McFunction parsedFunction, BacapAdvancement bacapAdvancement)
        : base(file, parsedFunction, bacapAdvancement)
    {

    }

    [PublicAPI]
    public override void Update()
    {
        var advId = BacapAdvancement.McPath;
        var rewardId = MinecraftUtils.StripNamespace(BacapAdvancement.Advancement.Rewards!.Function!);
        var tier = BacapAdvancement.Tier;

        var functionArguments = Snbt.Compound()
            .Put("adv_id", advId)
            .Put("reward_id", rewardId).Put((string)"tier", tier.TechnicalName)
            .Build();

        var newCommand = new FunctionCommand(BacapAdvancement.Datapack.Settings.MacroCommandName, functionArguments);


        // Find the existing command using type matching rather than fragile string parsing
        var existingIndex = Function.Lines.FindIndex(line =>
        {
            // Check if the line is an executable command (not a comment or empty space)
            if (line is not ExecutableLine executableLine)
                return line.Build().Contains(BacapAdvancement.Datapack.Settings.MacroCommandName, StringComparison.OrdinalIgnoreCase);

            // Safely check if the underlying command is specifically a FunctionCommand
            if (executableLine.Command is FunctionCommand fc)
                return fc.CommandName.Equals(BacapAdvancement.Datapack.Settings.MacroCommandName, StringComparison.OrdinalIgnoreCase);

            // Fallback: Check the raw string in case the parser failed to map it
            // (e.g., if the file was previously corrupted with NUL chars or multi-line breaks)
            return line.Build().Contains(BacapAdvancement.Datapack.Settings.MacroCommandName, StringComparison.OrdinalIgnoreCase);
        });

        if (existingIndex >= 0)
            Function.Lines[existingIndex] = newCommand.ToLine();
        else
        {
            // If the file consists only of empty lines or whitespace, clear it
            // so we don't leave random blank lines at the top of the generated file.
            if (Function.Lines.TrueForAll(line => line is EmptyLine))
                Function.Lines.Clear();

            Function.Lines.Add(newCommand.ToLine());
        }
    }
}