using BacapGenerator.Common;
using BacapGenerator.Configuration;
using BacapGenerator.Configuration.Exceptions;
using Core.Registries;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace BacapGenerator.Datapacks.Models.Settings;

/// <summary>
/// Represents the configuration, filesystem paths, and domain metadata for a datapack.
/// Supports direct binding from configuration providers.
/// </summary>
public sealed class DatapackSettings
{
    /// <summary>
    /// Gets the filesystem path to the root folder of the datapack.
    /// </summary>
    [ConfigurationKeyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the primary namespace containing the advancements.
    /// </summary>
    [ConfigurationKeyName("main_namespace")]
    public string MainNamespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the namespace used for custom rewards, functions, and internal macros.
    /// </summary>
    [ConfigurationKeyName("reward_namespace")]
    public string RewardNamespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the raw string representation of the datapack type as defined in config.yaml.
    /// </summary>
    [ConfigurationKeyName("type")]
    public string? RawType { get; init; }

    /// <summary>
    /// Gets the parsed and verified datapack operational type.
    /// </summary>
    public DatapackType DatapackType { get; private set; }

    /// <summary>
    /// Gets the precalculated macro command identifier.
    /// </summary>
    public string MacroCommandName => field ??= $"{RewardNamespace}:advancement_made_macro";

    /// <summary>
    /// Gets the optional identifier of the parent datapack when acting as an override addon.
    /// </summary>
    [PublicAPI]
    [ConfigurationKeyName("parent_datapack_id")]
    public string? ParentDatapackId { get; init; }

    [PublicAPI]
    [ConfigurationKeyName("fanpacks_namespace")]
    public string? FanpacksNamespace { get; init; }

    /// <summary>
    /// Validates the current datapack settings and verifies its integrity.
    /// </summary>
    /// <param name="datapackId">The key identifier of this datapack in the configuration dictionary.</param>
    /// <param name="minecraftData">The registry containing validated Minecraft entity and game data.</param>
    /// <exception cref="DatapackConfigurationException">Thrown when a setting rule is violated.</exception>
    /// <example>
    /// <code>
    /// datapackSettings.Validate("bacaped", minecraftData);
    /// </code>
    /// </example>
    public void Validate(string datapackId)
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            throw new DatapackConfigurationException(
                datapackId,
                DatapackErrorKind.MissingPath,
                $"Path is not defined for datapack '{datapackId}'.",
                nameof(Path));
        }

        DatapackType = ConfigParser.ParseRequiredEnum<DatapackType>(
            RawType,
            datapackId,
            DatapackErrorKind.InvalidDatapackType,
            missingErrorMessage: $"Missing 'type' property for datapack '{datapackId}'. Expected: reference, addon, compatibility_addon.",
            invalidErrorMessage: $"Invalid datapack type '{RawType}' in '{datapackId}'. Allowed values: reference, addon, compatibility_addon.",
            propertyName: "type");

        if (string.IsNullOrWhiteSpace(MainNamespace))
        {
            throw new DatapackConfigurationException(
                datapackId,
                DatapackErrorKind.MissingNamespace,
                $"Main namespace is missing for datapack '{datapackId}'.",
                "main_namespace");
        }

        if (string.IsNullOrWhiteSpace(RewardNamespace))
        {
            throw new DatapackConfigurationException(
                datapackId,
                DatapackErrorKind.MissingNamespace,
                $"Reward namespace is missing for datapack '{datapackId}'.",
                "reward_namespace");
        }

        if (DatapackType == DatapackType.CompatibilityAddon && string.IsNullOrWhiteSpace(ParentDatapackId))
        {
            throw new DatapackConfigurationException(
                datapackId,
                DatapackErrorKind.MissingParentDatapack,
                $"Compatibility addon '{datapackId}' must specify 'parent_datapack_id'.",
                "parent_datapack_id");
        }
    }
}