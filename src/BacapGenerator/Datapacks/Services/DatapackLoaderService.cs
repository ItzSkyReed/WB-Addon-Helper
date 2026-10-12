using System.Diagnostics;
using BacapGenerator.Datapacks.Models.Settings;
using Core.Registries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BacapGenerator.Datapacks.Services;

/// <summary>
/// The main application service that drives the datapack loading and generation process.
/// </summary>
/// <param name="options">Bound datapack settings mapped by configuration key.</param>
/// <param name="datapackFactory">Factory responsible for constructing populated datapack models.</param>
/// <param name="datapackRegistry">Central registry storing loaded datapack instances.</param>
/// <param name="logger">Logger instance for diagnostic and operational telemetry.</param>
public partial class DatapackLoaderService(
    IOptions<Dictionary<string, DatapackSettings>> options,
    IDatapackFactory datapackFactory,
    DatapackRegistry datapackRegistry,
    ILogger<DatapackLoaderService> logger)
{
    private readonly Dictionary<string, DatapackSettings> _datapackConfigs = options.Value;

    /// <summary>
    /// Executes the main logic for loading, validating, and resolving all configured datapacks.
    /// </summary>
    /// <exception cref="Exception">Propagates any fatal initialization or validation exception after logging.</exception>
    public void LoadAll()
    {
        LogFoundCountDatapacksToProcess(_datapackConfigs.Count);

        foreach (var (id, settings) in _datapackConfigs)
        {
            try
            {
                settings.Validate(id);

                LogProcessingDatapack(id, settings.Path, settings.DatapackType);

                var sw = Stopwatch.StartNew();

                var datapack = datapackFactory.Create(id, settings);
                datapackRegistry.Register(datapack);

                sw.Stop();

                LogSuccessfullyLoadedAdvancements(datapack.Advancements.Count, id);

                LogLoadedDatapack(id, sw.ElapsedMilliseconds, sw.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                LogFailedToProcessDatapack(id, ex);
                throw;
            }
        }

        ResolveOverrides();
    }

    /// <summary>
    /// Resolves cross-datapack parent-child links for addon overrides.
    /// </summary>
    private void ResolveOverrides()
    {
        LogResolvingDatapackOverrides();

        foreach (var childPack in datapackRegistry.Values)
        {
            if (childPack.Settings.ParentDatapackId is not { } parentId)
                continue;

            if (datapackRegistry.TryGet(parentId, out var parentPack))
            {
                DatapackResolver.ResolveOverrides(childPack, parentPack);
                LogSuccessfullyLinkedDatapacks(childPack.Id, parentPack.Id);
            }
            else
            {
                LogFailedToLinkDatapacks(parentId, childPack.Id);
            }
        }
    }

    [LoggerMessage(LogLevel.Information, "Found {Count} datapacks to process.")]
    partial void LogFoundCountDatapacksToProcess(int count);

    [LoggerMessage(LogLevel.Information, "Processing datapack '{DatapackId}' at '{Path}' (Mode: {DatapackType}).")]
    partial void LogProcessingDatapack(string datapackId, string path, DatapackType datapackType);

    [LoggerMessage(LogLevel.Information, "Successfully loaded {Count} advancements for '{DatapackId}'.")]
    partial void LogSuccessfullyLoadedAdvancements(int count, string datapackId);

    [LoggerMessage(LogLevel.Debug, "Datapack '{DatapackId}' loaded in {ElapsedMilliseconds} ms ({ElapsedSeconds:F2}s).")]
    partial void LogLoadedDatapack(string datapackId, long elapsedMilliseconds, double elapsedSeconds);

    [LoggerMessage(LogLevel.Error, "Failed to process datapack '{DatapackId}'.")]
    partial void LogFailedToProcessDatapack(string datapackId, Exception exception);

    [LoggerMessage(LogLevel.Information, "Resolving datapack overrides...")]
    partial void LogResolvingDatapackOverrides();

    [LoggerMessage(LogLevel.Information, "Successfully linked '{ChildDatapackId}' as an addon to '{ParentDatapackId}'.")]
    partial void LogSuccessfullyLinkedDatapacks(string childDatapackId, string parentDatapackId);

    [LoggerMessage(LogLevel.Warning, "Parent datapack '{ParentDatapackId}' for addon '{ChildDatapackId}' was not found in the registry.")]
    partial void LogFailedToLinkDatapacks(string parentDatapackId, string childDatapackId);
}