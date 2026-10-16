using System.Text;

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
    /// Parses a .env file natively, handling UTF-8/UTF-16 BOM, comments, quotes, and Windows backslashes safely.
    /// </summary>
    /// <param name="envFilePath">The path to the .env file to parse.</param>
    /// <returns>A case-insensitive dictionary of parsed key-value pairs, or <see langword="null"/> if the file does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="envFilePath"/> is null or empty.</exception>
    private static Dictionary<string, string>? ParseEnvFile(string envFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(envFilePath);

        if (!File.Exists(envFilePath))
            return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // StreamReader handles standard BOMs automatically
        using var reader = new StreamReader(envFilePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (reader.ReadLine() is { } rawLine)
        {
            // Fallback trim for any leftover BOMs just in case
            var line = rawLine.AsSpan().Trim().TrimStart('\uFEFF');

            if (line.IsEmpty || line[0] == '#')
                continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();

            // Strip surrounding quotes safely
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1].Trim();
            }

            result[key.ToString()] = value.ToString();
        }

        return result;
    }

    /// <summary>
    /// Resolves the final path by checking the OS environment first, then falling back to the .env file dictionary, and expanding any variables.
    /// </summary>
    /// <param name="key">The environment variable key to resolve.</param>
    /// <param name="fileEnv">The parsed dictionary from the .env file.</param>
    /// <returns>The fully expanded and resolved absolute path, or <see langword="null"/> if not found.</returns>
    private static string? ResolvePath(string key, Dictionary<string, string>? fileEnv)
    {
        // Check standard OS environment variable
        var rawValue = Environment.GetEnvironmentVariable(key);

        // Fall back to parsed .env file entries
        if (string.IsNullOrWhiteSpace(rawValue) && fileEnv != null)
            fileEnv.TryGetValue(key, out rawValue);

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