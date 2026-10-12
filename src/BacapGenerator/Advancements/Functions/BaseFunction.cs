using BacapGenerator.Advancements.Models;
using Core.McFunctions.Models;
using Core.Registries;
using JetBrains.Annotations;

namespace BacapGenerator.Advancements.Functions;

/// <summary>
/// Base class for all loaded advancements in the workspace.
/// Contains purely in-memory state.
/// </summary>
public abstract class BaseFunction
{
    protected BacapAdvancement BacapAdvancement { get; }

    [PublicAPI]
    public McFunction Function { get; set; }

    /// <summary>
    /// Gets the original file path before any unsaved modifications.
    /// Used by the IO Manager to clean up old files when paths are changed.
    /// </summary>
    public FileInfo OriginalFile { get; internal set; }

    /// <summary>
    /// Gets or sets the intended file path. Changing this does NOT affect the file system immediately.
    /// </summary>
    [PublicAPI]
    public FileInfo File { get; set; }

    internal BaseFunction(FileInfo file, McFunction parsedFunction, BacapAdvancement bacapAdvancement)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(parsedFunction);
        ArgumentNullException.ThrowIfNull(bacapAdvancement);

        BacapAdvancement = bacapAdvancement;

        File = file;
        OriginalFile = file;

        Function = parsedFunction;
    }

    /// <summary>
    /// Updates the function's internal AST state based on the current state of the parent Advancement.
    /// </summary>
    [PublicAPI]
    public abstract void Update();

    public override string ToString() => $"{GetType().Name}({File.Name})";
}