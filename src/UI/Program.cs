using System.Text;
using BacapGenerator.Configuration;
using BacapGenerator.Datapacks.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UI.Configuration;
using UI.Diagnostics;
using UI.Menus;
using UI.Startup;
using UI.Styling;

Console.OutputEncoding = Encoding.UTF8;

const string defaultConfigFileName = "wb_config.yaml";
const string embeddedConfigResourceName = "UI.default_wb_config.yaml";

var isVerbose = args.Contains("--verbose", StringComparer.OrdinalIgnoreCase)
                || args.Contains("-v", StringComparer.OrdinalIgnoreCase)
                || args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

// Extract custom config path if passed via CLI, otherwise fallback to default
var configPath = GetCliOption(args, "--config", "-c") ?? defaultConfigFileName;
var resolvedConfigPath = Path.GetFullPath(configPath);

try
{
    // Ensure parent directory exists if a path with subfolders was specified
    var configDirectory = Path.GetDirectoryName(resolvedConfigPath);
    if (!string.IsNullOrEmpty(configDirectory) && !Directory.Exists(configDirectory))
    {
        Directory.CreateDirectory(configDirectory);
    }

    // Ensure configuration file exists on disk
    YamlConfigBootstrapper.EnsureConfigExists(resolvedConfigPath, embeddedConfigResourceName);

    // Build host and register dependencies
    using var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            // Pass the absolute resolved path to prevent resolving against AppContext.BaseDirectory
            config.AddYamlFile(resolvedConfigPath, optional: false, reloadOnChange: true);
            config.ExpandTemplates();
        })
        .ConfigureServices((context, services) =>
        {
            services.AddApplicationConfiguration(context.Configuration)
                .AddUserConfiguration()
                .AddMinecraftRegistries(context.Configuration)
                .AddDatapackServices()
                .AddUiActions();
        })
        .Build();

    // Eagerly load, validate and link all configured datapacks
    var datapackLoader = host.Services.GetRequiredService<DatapackLoaderService>();
    datapackLoader.LoadAll();

    // Launch main interactive menu
    var mainMenu = host.Services.GetRequiredService<MainMenuAction>();
    await mainMenu.ExecuteAsync();

    return 0;
}
catch (Exception ex)
{
    var exitCode = AppErrorHandler.Handle(ex, showStackTrace: isVerbose);
    TuiTheme.WaitForKey();
    return exitCode;
}

/// <summary>
/// Retrieves the value of a command-line option by matching provided flag names.
/// Supports both space-separated (e.g., <c>--config path.yaml</c>) and equals-separated (e.g., <c>--config=path.yaml</c>) formats.
/// </summary>
/// <param name="arguments">The collection of command-line arguments to search.</param>
/// <param name="flags">The flag aliases to match (e.g., <c>"--config"</c>, <c>"-c"</c>).</param>
/// <returns>The extracted parameter value, or <see langword="null"/> if the flag is absent or value is missing.</returns>
/// <example>
/// <code>
/// string? customConfig = GetCliOption(args, "--config", "-c");
/// </code>
/// </example>
static string? GetCliOption(string[] arguments, params string[] flags)
{
    for (var i = 0; i < arguments.Length; i++)
    {
        var current = arguments[i];

        foreach (var flag in flags)
        {
            // Case 1: --config=custom.yaml
            if (current.StartsWith($"{flag}=", StringComparison.OrdinalIgnoreCase))
            {
                var value = current[(flag.Length + 1)..].Trim('"', '\'');
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            // Case 2: --config custom.yaml
            if (!string.Equals(current, flag, StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 < arguments.Length && !arguments[i + 1].StartsWith('-'))
            {
                return arguments[i + 1].Trim('"', '\'');
            }

            return null;
        }
    }

    return null;
}