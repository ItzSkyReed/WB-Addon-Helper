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
    public static UserConfig Load(string envFilePath = ".env")
    {
        var envValues = ParseEnvFile(envFilePath);

        return new UserConfig
        {
            WorldDatapacksPath = ResolvePath(WorldDatapacksEnvKey, envValues),
        };
    }

    /// <summary>
    /// Parses a .env file natively, handling UTF-8 BOM, comments, quotes, and Windows backslashes safely.
    /// </summary>
    private static Dictionary<string, string>? ParseEnvFile(string envFilePath)
    {
        if (!File.Exists(envFilePath))
            return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // File.ReadLines automatically strips UTF-8 BOM and reads lazily
        foreach (var rawLine in File.ReadLines(envFilePath))
        {
            var line = rawLine.AsSpan().Trim().TrimStart('\uFEFF');
            if (line.IsEmpty || line[0] == '#')
                continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();

            // Strip surrounding quotes without corrupting Windows backslashes
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1].Trim();
            }

            result[key.ToString()] = value.ToString();
        }

        return result;
    }

    private static string? ResolvePath(string key, Dictionary<string, string>? fileEnv)
    {
        // Check standard OS environment variable
        var rawValue = Environment.GetEnvironmentVariable(key);

        // Fallback for cmd.exe `for /f` bug when reading UTF-8 with BOM on line 1 (prepends "\uFEFF" or "ï»¿" to the first variable name)
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            rawValue = Environment.GetEnvironmentVariable("\uFEFF" + key)
                       ?? Environment.GetEnvironmentVariable("ï»¿" + key);
        }

        // Fall back to parsed .env file entries
        if (string.IsNullOrWhiteSpace(rawValue) && fileEnv != null && fileEnv.TryGetValue(key, out var fileValue))
            rawValue = fileValue;

        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

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
            return expandedPath;
        }
    }
}