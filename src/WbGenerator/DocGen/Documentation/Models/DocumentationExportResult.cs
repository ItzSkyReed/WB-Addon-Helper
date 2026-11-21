namespace WbGenerator.DocGen.Documentation.Models;

/// <summary>
/// Represents the outcome of a WorldBorder documentation export run.
/// </summary>
public sealed record DocumentationExportResult
{
    /// <summary>
    /// Gets a value indicating whether the export completed without errors (including skipped runs).
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets a value indicating whether documentation export was skipped because it is not configured.
    /// </summary>
    public bool WasSkipped { get; init; }

    /// <summary>
    /// Gets the full path to the generated JSON file when export succeeds.
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// Gets the number of advancement entries written to the documentation JSON file.
    /// </summary>
    public int ExportedCount { get; init; }

    /// <summary>
    /// Gets the number of excluded advancements skipped during export.
    /// </summary>
    public int SkippedExcludedCount { get; init; }

    /// <summary>
    /// Gets an informational or diagnostic message explaining why the export was skipped or failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a result representing a skipped export run.
    /// </summary>
    /// <param name="reason">The human-readable reason why export was skipped.</param>
    /// <returns>A skipped <see cref="DocumentationExportResult"/>.</returns>
    public static DocumentationExportResult Skipped(string reason) => new()
    {
        IsSuccess = true,
        WasSkipped = true,
        ErrorMessage = reason
    };

    /// <summary>
    /// Creates a result representing a successful documentation export.
    /// </summary>
    /// <param name="outputPath">The full filesystem path to the generated JSON file.</param>
    /// <param name="exportedCount">The number of exported advancement entries.</param>
    /// <param name="skippedExcludedCount">The number of excluded advancements skipped.</param>
    /// <returns>A successful <see cref="DocumentationExportResult"/>.</returns>
    public static DocumentationExportResult Success(
        string outputPath,
        int exportedCount,
        int skippedExcludedCount) => new()
    {
        IsSuccess = true,
        OutputPath = outputPath,
        ExportedCount = exportedCount,
        SkippedExcludedCount = skippedExcludedCount
    };

    /// <summary>
    /// Creates a result representing a failed documentation export.
    /// </summary>
    /// <param name="errorMessage">The error description.</param>
    /// <returns>A failed <see cref="DocumentationExportResult"/>.</returns>
    public static DocumentationExportResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}