using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace BacapGenerator.Configuration;

/// <summary>
/// Configuration settings controlling JSON documentation export output paths and source datapacks.
/// </summary>
public sealed class DocumentGeneratorConfig
{
    /// <summary>
    /// Gets the filesystem directory where generated documentation JSON files will be written.
    /// </summary>
    [PublicAPI]
    [ConfigurationKeyName("output_directory")]
    public string OutputDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the output JSON file name for the exported documentation.
    /// Defaults to <c>wb_addon.json</c>.
    /// </summary>
    [PublicAPI]
    [ConfigurationKeyName("output_file_name")]
    public string OutputFileName { get; init; } = "wb_addon.json";

    /// <summary>
    /// Gets the prioritized list of datapack IDs from the registry to include in documentation export.
    /// When empty, all loaded <c>reference</c> datapacks are included in alphabetical order.
    /// </summary>
    [PublicAPI]
    [ConfigurationKeyName("datapacks")]
    public List<string> Datapacks { get; init; } = [];

    /// <summary>
    /// Validates the documentation generator configuration settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="OutputDirectory"/> or <see cref="OutputFileName"/> is null, empty, or whitespace,
    /// or when <see cref="Datapacks"/> contains blank entries.
    /// </exception>
    /// <example>
    /// <code>
    /// globalConfig.DocumentGenerator?.Validate();
    /// </code>
    /// </example>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputDirectory))
            throw new InvalidOperationException("'document_generator.output_directory' must be provided in config.");

        if (string.IsNullOrWhiteSpace(OutputFileName))
            throw new InvalidOperationException("'document_generator.output_file_name' cannot be empty.");

        if (Datapacks.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("'document_generator.datapacks' cannot contain empty entries.");
    }
}