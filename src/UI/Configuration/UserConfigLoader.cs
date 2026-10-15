using dotenv.net;

namespace UI.Configuration;

/// <summary>
/// Provides functionality to load and construct <see cref="UserConfig"/> from system environment variables and an optional .env file.
/// </summary>
public static class UserConfigLoader
{
    private const string WorldDatapacksEnvKey = "WORLD_DATAPACKS_PATH";

    /// <summary>
    /// Loads configuration settings, prioritizing actual system environment variables over values defined in the .env file.
    /// </summary>
    /// <param name="envFilePath">The file path to the environment file. Defaults to <c>".env"</c>.</param>
    /// <returns>A populated <see cref="UserConfig"/> instance.</returns>
    /// <example>
    /// <code>
    /// UserConfig config = UserConfigLoader.Load();
    /// </code>
    /// </example>
    public static UserConfig Load(string envFilePath = ".env")
    {
        var envValues = ReadEnvFile(envFilePath);

        return new UserConfig
        {
            WorldDatapacksPath = ResolvePath(WorldDatapacksEnvKey, envValues)
        };
    }

    /// <summary>
    /// Reads and sanitizes key-value pairs from the specified .env file if it exists.
    /// </summary>
    /// <param name="envFilePath">The path to the .env file.</param>
    /// <returns>A case-insensitive dictionary of environment variables, or <see langword="null"/> if the file does not exist.</returns>
    private static Dictionary<string, string>? ReadEnvFile(string envFilePath)
    {
        if (!File.Exists(envFilePath))
            return null;

        var rawEnv = DotEnv.Read(new DotEnvOptions(
            envFilePaths: [envFilePath],
            ignoreExceptions: true,
            trimValues: true));

        // Sanitize keys in case the .env file was saved with a UTF-8 BOM (\uFEFF) on the first line
        var envValues = new Dictionary<string, string>(rawEnv.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in rawEnv)
        {
            var cleanKey = key.Trim().TrimStart('\uFEFF');
            envValues[cleanKey] = value;
        }

        return envValues;
    }

    /// <summary>
    /// Resolves an environment setting from process environment variables, falling back to parsed .env file values,
    /// strips surrounding quotes, and expands any embedded system variables (e.g., %APPDATA%).
    /// </summary>
    /// <param name="key">The environment variable key name.</param>
    /// <param name="fileEnv">The parsed key-value pairs from the .env file, if available.</param>
    /// <returns>A normalized full directory path, or <see langword="null"/> if not configured or invalid.</returns>
    private static string? ResolvePath(string key, IReadOnlyDictionary<string, string>? fileEnv)
    {
        // Process / OS environment variables take precedence
        var rawValue = Environment.GetEnvironmentVariable(key);

        // Fall back to .env file entries
        if (string.IsNullOrWhiteSpace(rawValue) && fileEnv != null && fileEnv.TryGetValue(key, out var fileValue))
            rawValue = fileValue;

        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        // Trim whitespace and strip matching or stray surrounding quotes (' or ")
        var sanitizedValue = rawValue.Trim().Trim('\'', '"').Trim();
        if (string.IsNullOrWhiteSpace(sanitizedValue))
            return null;

        var expandedPath = Environment.ExpandEnvironmentVariables(sanitizedValue);

        try
        {
            return Path.GetFullPath(expandedPath);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // Return the raw expanded string if Path.GetFullPath fails so the UI can still display the broken value
            return expandedPath;
        }
    }
}