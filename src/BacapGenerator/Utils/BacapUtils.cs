using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace BacapGenerator.Utils;

public static class BacapUtils
{
    /// <summary>
    /// Extracts the tab segment (first path component) from a resource location string.
    /// </summary>
    /// <param name="resourceLocation">The raw resource location (e.g., "blazeandcave:weaponry/kill_mob").</param>
    /// <returns>The extracted tab string, or <see langword="null"/> if the location is null or invalid.</returns>
    /// <example>
    /// <code>
    /// string? tab = BacapUtils.ExtractTab("blazeandcave:weaponry/kill_mob"); // "weaponry"
    /// </code>
    /// </example>
    public static string? ExtractTab(string? resourceLocation)
    {
        if (string.IsNullOrWhiteSpace(resourceLocation))
            return null;

        var tabSpan = ExtractTab(resourceLocation.AsSpan());
        return tabSpan.IsEmpty ? null : tabSpan.ToString();
    }

    /// <summary>
    /// Extracts the tab segment (first path component) from a resource location span with zero allocations.
    /// </summary>
    /// <param name="resourceLocation">The raw resource location span.</param>
    /// <returns>A span containing the tab name, or an empty span if invalid.</returns>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;char&gt; tab = BacapUtils.ExtractTab("blazeandcave:weaponry/kill_mob".AsSpan());
    /// </code>
    /// </example>
    public static ReadOnlySpan<char> ExtractTab(ReadOnlySpan<char> resourceLocation)
    {
        if (resourceLocation.IsWhiteSpace())
            return ReadOnlySpan<char>.Empty;

        var path = MinecraftUtils.GetPathSpan(resourceLocation);
        var slashIndex = path.IndexOf('/');

        return slashIndex >= 0 ? path[..slashIndex] : path;
    }

    /// <summary>
    /// Tries to extract the tab segment from a resource location string.
    /// </summary>
    /// <param name="resourceLocation">The raw resource location (e.g., "blazeandcave:weaponry/kill_mob").</param>
    /// <param name="tab">When this method returns, contains the extracted tab name if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the tab was successfully extracted; otherwise, <see langword="false"/>.</returns>
    /// <example>
    /// <code>
    /// if (BacapUtils.TryExtractTab("blazeandcave:weaponry/kill_mob", out var tab))
    /// {
    ///     Console.WriteLine(tab); // "weaponry"
    /// }
    /// </code>
    /// </example>
    public static bool TryExtractTab(string? resourceLocation, [NotNullWhen(true)] out string? tab)
    {
        tab = ExtractTab(resourceLocation);
        return tab is not null;
    }

    /// <summary>
    /// Tries to extract the tab segment from a resource location span with zero allocations.
    /// </summary>
    /// <param name="resourceLocation">The raw resource location span.</param>
    /// <param name="tab">When this method returns, contains the extracted tab span if successful; otherwise, an empty span.</param>
    /// <returns><see langword="true"/> if the tab was successfully extracted and is not empty; otherwise, <see langword="false"/>.</returns>
    /// <example>
    /// <code>
    /// if (BacapUtils.TryExtractTab("blazeandcave:weaponry/kill_mob".AsSpan(), out ReadOnlySpan&lt;char&gt; tab))
    /// {
    ///     // Process tab
    /// }
    /// </code>
    /// </example>
    public static bool TryExtractTab(ReadOnlySpan<char> resourceLocation, out ReadOnlySpan<char> tab)
    {
        tab = ExtractTab(resourceLocation);
        return !tab.IsEmpty;
    }

    /// <summary>
    /// Converts a string into a lower_snake_case slug, stripping punctuation/symbols
    /// (while collapsing word-internal apostrophes like "Hanna's" -> "hannas")
    /// and collapsing consecutive separators into a single underscore.
    /// </summary>
    /// <param name="input">The input string to slugify.</param>
    /// <returns>
    /// A lower_snake_case formatted string without leading or trailing underscores,
    /// or <see cref="string.Empty"/> if the input contains no letters or digits.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// string result1 = BacapUtils.ToSnakeCaseSlug("Hello World"); // "hello_world"
    /// string result2 = BacapUtils.ToSnakeCaseSlug("Hanna's Day"); // "hannas_day"
    /// </code>
    /// </example>
    public static string ToSnakeCaseSlug(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length == 0)
            return string.Empty;

        const int maxStackLimit = 64;

        var buffer = input.Length <= maxStackLimit
            ? stackalloc char[input.Length]
            : new char[input.Length];

        var writeIndex = 0;
        var lastWasSeparator = true; // Prevents leading underscores

        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer[writeIndex++] = char.ToLower(c, CultureInfo.InvariantCulture);
                lastWasSeparator = false;
            }
            else if (c is '\'' or '’' && !lastWasSeparator)
            {
                // Ignore apostrophes inside words.
            }
            else if (!lastWasSeparator)
            {
                buffer[writeIndex++] = '_';
                lastWasSeparator = true;
            }
        }

        // Trim trailing underscore if present
        if (writeIndex > 0 && buffer[writeIndex - 1] == '_')
            writeIndex--;

        return writeIndex == 0 ? string.Empty : new string(buffer[..writeIndex]);
    }
}