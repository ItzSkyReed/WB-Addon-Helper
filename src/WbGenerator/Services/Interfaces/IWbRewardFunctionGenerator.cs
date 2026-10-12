using WbGenerator.Models;

namespace WbGenerator.Services.Interfaces;

/// <summary>
/// Contract responsible for synthesizing individual advancement reward .mcfunction files
/// using Minecraft command models.
/// </summary>
public interface IWbRewardFunctionGenerator
{
    /// <summary>
    /// Generates and writes the reward function file for the specified advancement into the target datapack folder.
    /// </summary>
    /// <param name="context">The contextual reward and metadata parameters.</param>
    /// <param name="datapackRootDirectory">The root directory of the datapack being generated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous generation process.</returns>
    /// <exception cref="IOException">Thrown when an error occurs during file writing.</exception>
    Task GenerateRewardFunctionAsync(
        RewardGenerationContext context,
        string datapackRootDirectory,
        CancellationToken cancellationToken = default);
}