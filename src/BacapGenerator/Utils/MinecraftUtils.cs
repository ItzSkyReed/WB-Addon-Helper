using System.Buffers;
using System.Text;
using JetBrains.Annotations;

namespace BacapGenerator.Utils;

public static class MinecraftUtils
{
    /// <summary>
    /// Vectorized search values for fast path separator matching in .NET 10.
    /// </summary>
    private static readonly SearchValues<char> PathSeparators = SearchValues.Create('/', '\\');

    private const string DefaultNamespace = "minecraft";

    /// <summary>
    /// Converts a file system path (starting from the namespace directory) into a Minecraft resource location format.
    /// </summary>
    /// <param name="relativePath">
    /// The relative file path starting from the namespace directory
    /// (e.g., <c>"custom/function/example/test.mcfunction"</c> or <c>"custom\\function\\test.mcfunction"</c>).
    /// </param>
    /// <returns>A Minecraft resource location string (e.g., <c>"custom:example/test"</c>).</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the path is null, empty, whitespace, or does not contain enough segments (at least namespace, category, and file).
    /// </exception>
    /// <example>
    /// <code>
    /// string mcPath = MinecraftUtils.ToMinecraftPath("custom/function/example/test.mcfunction");
    /// // mcPath is "custom:example/test"
    /// </code>
    /// </example>
    public static string ToMinecraftPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var span = relativePath.AsSpan();

        // Locate namespace (first segment)
        var firstSep = span.IndexOfAny(PathSeparators);
        if (firstSep <= 0)
            throw new ArgumentException("Path must contain a namespace directory.", nameof(relativePath));

        // Locate category directory to skip (e.g. 'function', 'tags', 'advancement')
        var secondSep = span[(firstSep + 1)..].IndexOfAny(PathSeparators);
        if (secondSep < 0)
            throw new ArgumentException("Path must contain a category folder (e.g., 'function') and a target file.", nameof(relativePath));

        var subpathStart = firstSep + 1 + secondSep + 1;
        var subpathSpan = span[subpathStart..];
        if (subpathSpan.IsEmpty)
            throw new ArgumentException("Path does not contain a valid target file.", nameof(relativePath));

        // Strip the file extension from the filename
        var lastSlashInSubpath = subpathSpan.LastIndexOfAny(PathSeparators);
        var searchDotStart = lastSlashInSubpath >= 0 ? lastSlashInSubpath + 1 : 0;
        var dotIndexInFilename = subpathSpan[searchDotStart..].LastIndexOf('.');

        var subpathLen = dotIndexInFilename >= 0
            ? searchDotStart + dotIndexInFilename
            : subpathSpan.Length;

        var totalLength = firstSep + 1 + subpathLen;

        // Single-pass string allocation with zero intermediate buffers
        return string.Create(totalLength, (relativePath, firstSep, subpathStart, subpathLen), static (dest, state) =>
        {
            var (source, nsLen, subStart, subLen) = state;
            var sourceSpan = source.AsSpan();

            // Copy namespace
            sourceSpan[..nsLen].CopyTo(dest);

            // Append ':' delimiter
            dest[nsLen] = ':';

            // Copy and normalize subpath (replace '\' with '/')
            var srcSub = sourceSpan.Slice(subStart, subLen);
            var destSub = dest[(nsLen + 1)..];

            for (var i = 0; i < srcSub.Length; i++)
            {
                var c = srcSub[i];
                destSub[i] = c == '\\' ? '/' : c;
            }
        });
    }

    /// <summary>
    /// Constructs an expected physical file path for a Minecraft resource location without requiring
    /// the target directory or file to already exist on disk.
    /// </summary>
    /// <param name="datapackDataPath">The root <c>data</c> directory path of the datapack.</param>
    /// <param name="category">The registry/category folder (e.g., "advancement", "function").</param>
    /// <param name="resourceLocation">The resource location (e.g., "my_namespace:monsters/mounted_menace").</param>
    /// <param name="extension">The file extension including the leading dot (e.g., ".json").</param>
    /// <returns>The constructed full physical path.</returns>
    /// <exception cref="ArgumentException">Thrown when any argument is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// string path = MinecraftUtils.BuildPhysicalPath("C:/Pack/data", "advancement", "bacap:monsters/test", ".json");
    /// // Returns "C:\Pack\data\bacap\advancement\monsters\test.json"
    /// </code>
    /// </example>
    [PublicAPI]
    public static string BuildPhysicalPath(
        string datapackDataPath,
        string category,
        string resourceLocation,
        string extension = ".json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datapackDataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceLocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var span = resourceLocation.AsSpan();
        var ns = GetNamespaceSpan(span).ToString();
        var relativePath = GetPathSpan(span).ToString().Replace('/', Path.DirectorySeparatorChar);

        var normalizedExt = extension.StartsWith('.') ? extension : $".{extension}";

        return Path.Combine(datapackDataPath, ns, category, $"{relativePath}{normalizedExt}");
    }

    /// <summary>
    /// Resolves a Minecraft resource location to an absolute physical file path within a datapack.
    /// Because resource locations lack file extensions, this method searches the target directory for a matching file.
    /// </summary>
    /// <param name="datapackDataPath">The root directory path of the datapack.</param>
    /// <param name="category">The registry/category folder (e.g., "advancement", "function", "tags").</param>
    /// <param name="resourceLocation">The resource location (e.g., "my_namespace:my_functions/my_function").</param>
    /// <returns>The fully qualified physical path to the existing file.</returns>
    /// <exception cref="ArgumentException">Thrown if arguments are invalid or empty.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown if the target directory does not exist in the datapack.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file cannot be found.</exception>
    /// <example>
    /// <code>
    /// string physicalPath = MinecraftUtils.ResolvePhysicalPath("C:/MyDatapack", "function", "bacap:weaponry/kill_mob");
    /// // Returns "C:\MyDatapack\data\bacap\function\weaponry\kill_mob.mcfunction"
    /// </code>
    /// </example>
    public static string ResolvePhysicalPath(string datapackDataPath, string category, string resourceLocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datapackDataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceLocation);

        var span = resourceLocation.AsSpan();
        var ns = GetNamespaceSpan(span).ToString();
        var relativePath = GetPathSpan(span).ToString();

        // Normalize slashes for the current OS
        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

        // datapack_root/data/namespace/category/relative/path
        var expectedPathWithoutExt = Path.Combine(datapackDataPath, ns, category, relativePath);

        var directory = Path.GetDirectoryName(expectedPathWithoutExt);
        var fileNameWithoutExt = Path.GetFileName(expectedPathWithoutExt);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"The directory '{directory}' does not exist in the datapack.");

        // ReSharper disable once GrammarMistakeInComment
        // Search for a file with the exact name but any extension (e.g. .json, .mcfunction, .nbt)
        foreach (var file in Directory.EnumerateFiles(directory, $"{fileNameWithoutExt}.*"))
        {
            // Ensure exact match of the file name (ignoring extension) to prevent partial matches
            // if someone named a file "my_function_2.json" and we search for "my_function.*"
            if (Path.GetFileNameWithoutExtension(file).Equals(fileNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        throw new FileNotFoundException($"Cannot find file matching '{fileNameWithoutExt}.*' in '{directory}'.");
    }


    /// <summary>
    /// Extracts the path part of a resource location (everything after the colon).
    /// </summary>
    /// <param name="resourceLocation">The raw resource location span.</param>
    /// <returns>The path segment span with zero allocations.</returns>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;char&gt; path = MinecraftUtils.GetPathSpan("minecraft:item/stick".AsSpan()); // "item/stick"
    /// </code>
    /// </example>
    public static ReadOnlySpan<char> GetPathSpan(ReadOnlySpan<char> resourceLocation)
    {
        if (resourceLocation.IsWhiteSpace())
            return ReadOnlySpan<char>.Empty;

        var colonIndex = resourceLocation.IndexOf(':');
        return colonIndex >= 0 ? resourceLocation[(colonIndex + 1)..] : resourceLocation;
    }

    /// <summary>
    /// Ensures that a resource location has a namespace, prepending the default namespace if none is present.
    /// </summary>
    /// <param name="resourceLocation">The resource location (e.g., "stick", "minecraft:stick", or "blazeandcave:weaponry").</param>
    /// <param name="defaultNamespace">The default namespace to apply if missing. Defaults to "minecraft".</param>
    /// <returns>The normalized resource location with a namespace.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="resourceLocation"/> is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// string item1 = MinecraftUtils.EnsureNamespace("stick"); // "minecraft:stick"
    /// string item2 = MinecraftUtils.EnsureNamespace("minecraft:stick"); // "minecraft:stick"
    /// string item3 = MinecraftUtils.EnsureNamespace("blazeandcave:weaponry"); // "blazeandcave:weaponry"
    /// </code>
    /// </example>
    public static string EnsureNamespace(string resourceLocation, string defaultNamespace = "minecraft")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceLocation);

        return resourceLocation.Contains(':')
            ? resourceLocation
            : $"{defaultNamespace}:{resourceLocation}";
    }

    /// <summary>
    /// Efficiently extracts the texture hash from a Base64-encoded Minecraft skin payload
    /// with zero intermediate heap allocations.
    /// </summary>
    /// <param name="base64Payload">The Base64 string from the profile properties.</param>
    /// <returns>The 64-character texture hash.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="base64Payload"/> is whitespace.</exception>
    /// <exception cref="FormatException">Thrown when Base64 is invalid or the texture URL marker is missing.</exception>
    /// <example>
    /// <code>
    /// string b64 = "eyJ0ZXh0dXJlcyI6eyJTS0lOIjp7InVybCI6Imh0dHA6Ly90ZXh0dXJlcy5taW5lY3JhZnQubmV0L3RleHR1cmUvNWM2ZDVhYmJmNjhjY2IyMzg2YmYxNmFmMjVhYzM4ZDhiNzdiYjBlMDQzMTUyNDYxYmQ5N2YzZjYzMGRiYjhiYyJ9fX0=";
    /// string hash = MinecraftTextureUtils.ExtractTextureHash(b64); // "5c6d5abbf68ccb2386bf16af25ac38d8b77bb0e043152461bd97f3f630dbb8bc"
    /// </code>
    /// </example>
    public static string ExtractTextureHash(ReadOnlySpan<char> base64Payload)
    {
        if (base64Payload.IsWhiteSpace())
            throw new ArgumentException("The Base64 payload cannot be null or whitespace.", nameof(base64Payload));

        // Standard skin payload is ~150-250 bytes decoded; 512 bytes is safe.
        var maxByteCount = base64Payload.Length * 3 / 4;
        byte[]? rented = null;
        var buffer = maxByteCount <= 512
            ? stackalloc byte[512]
            : rented = ArrayPool<byte>.Shared.Rent(maxByteCount);

        try
        {
            if (!Convert.TryFromBase64Chars(base64Payload, buffer, out int bytesWritten))
                throw new FormatException("Invalid Base64 format.");

            ReadOnlySpan<byte> decoded = buffer[..bytesWritten];
            var marker = "/texture/"u8;

            var markerIndex = decoded.IndexOf(marker);
            if (markerIndex < 0)
                throw new FormatException("The payload does not contain a valid Minecraft texture URL.");

            var remainder = decoded[(markerIndex + marker.Length)..];
            var endQuoteIndex = remainder.IndexOf((byte)'"');
            var hashBytes = endQuoteIndex >= 0 ? remainder[..endQuoteIndex] : remainder;

            return Encoding.UTF8.GetString(hashBytes);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Strips the namespace from a resource location string.
    /// </summary>
    /// <param name="resourceLocation">The resource location (e.g., "blazeandcave:weaponry/sub/name").</param>
    /// <returns>The path portion without the namespace.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="resourceLocation"/> is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// string path = MinecraftUtils.StripNamespace("blazeandcave:weaponry/test"); // "weaponry/test"
    /// </code>
    /// </example>
    public static string StripNamespace(string resourceLocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceLocation);
        return GetPathSpan(resourceLocation.AsSpan()).ToString();
    }

    /// <summary>
    /// Extracts the namespace from a resource location string.
    /// Defaults to <c>"minecraft"</c> if no colon is present.
    /// </summary>
    /// <param name="resourceLocation">The resource location string (e.g., "blazeandcave:weaponry/sub/name").</param>
    /// <param name="defaultNamespace">The fallback namespace to use when no colon is found.</param>
    /// <returns>The extracted namespace string.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="resourceLocation"/> is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// string ns = MinecraftUtils.GetNamespace("blazeandcave:weaponry/test"); // "blazeandcave"
    /// string fallbackNs = MinecraftUtils.GetNamespace("stick"); // "minecraft"
    /// </code>
    /// </example>
    public static string GetNamespace(
        string resourceLocation,
        string defaultNamespace = "minecraft")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceLocation);
        return GetNamespaceSpan(resourceLocation.AsSpan(), defaultNamespace).ToString();
    }

    /// <summary>
    /// Splits a resource location into a named tuple of namespace and path strings.
    /// Optimized to perform only the minimum necessary string allocations.
    /// </summary>
    /// <param name="resourceLocation">The resource location string (e.g., "blazeandcave:weaponry/test").</param>
    /// <param name="defaultNamespace">The fallback namespace to use when no colon is found.</param>
    /// <returns>A named tuple containing the <c>Namespace</c> and <c>Path</c> strings.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="resourceLocation"/> is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// var (ns, path) = MinecraftUtils.SplitResourceLocation("blazeandcave:weaponry/test");
    /// // ns   -> "blazeandcave"
    /// // path -> "weaponry/test"
    /// </code>
    /// </example>
    public static (string Namespace, string Path) SplitResourceLocation(
        string resourceLocation,
        string defaultNamespace = "minecraft")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceLocation);

        var span = resourceLocation.AsSpan();
        var colonIndex = span.IndexOf(':');

        return colonIndex >= 0
            ? (span[..colonIndex].ToString(), span[(colonIndex + 1)..].ToString())
            : (defaultNamespace, resourceLocation);
    }

    /// <summary>
    /// Replaces the first path segment (the tab) of a resource location with a new segment.
    /// </summary>
    /// <param name="resourceLocation">The source resource location (e.g., "blazeandcave:weaponry/kill_mob").</param>
    /// <param name="newSegment">The new tab/folder segment to replace with (e.g., "adventure").</param>
    /// <returns>The updated resource location string.</returns>
    /// <exception cref="ArgumentException">Thrown if any parameter is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// string updated = MinecraftUtils.ReplaceFirstPathSegment("bacap:weaponry/kill_mob", "adventure");
    /// // Returns "bacap:adventure/kill_mob"
    /// </code>
    /// </example>
    public static string ReplaceFirstPathSegment(string resourceLocation, string newSegment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceLocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(newSegment);

        var span = resourceLocation.AsSpan();
        var ns = GetNamespaceSpan(span);
        var path = GetPathSpan(span);

        var firstSlash = path.IndexOf('/');
        var remainder = firstSlash >= 0 ? path[firstSlash..] : ReadOnlySpan<char>.Empty;

        return $"{ns}:{newSegment}{remainder}";
    }

    /// <summary>
    /// Extracts the namespace from a resource location. Defaults to "minecraft" if not specified.
    /// </summary>
    /// <param name="resourceLocation">The raw resource location.</param>
    /// <param name="fallbackNamespace">Fallback namespace when no colon is present.</param>
    /// <returns>The namespace span.</returns>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;char&gt; ns = MinecraftUtils.GetNamespaceSpan("bacap:adventure/root".AsSpan()); // "bacap"
    /// </code>
    /// </example>
    [PublicAPI]
    public static ReadOnlySpan<char> GetNamespaceSpan(
        ReadOnlySpan<char> resourceLocation,
        ReadOnlySpan<char> fallbackNamespace = default)
    {
        if (resourceLocation.IsWhiteSpace())
            return ReadOnlySpan<char>.Empty;

        var colonIndex = resourceLocation.IndexOf(':');
        if (colonIndex >= 0)
            return resourceLocation[..colonIndex];

        return fallbackNamespace.IsEmpty ? DefaultNamespace : fallbackNamespace;
    }
}