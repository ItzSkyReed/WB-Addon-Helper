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

const string configFileName = "wb_config.yaml";
const string embeddedConfigResourceName = "UI.default_wb_config.yaml";

var isVerbose = args.Contains("--verbose", StringComparer.OrdinalIgnoreCase)
                || args.Contains("-v", StringComparer.OrdinalIgnoreCase)
                || args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

try
{
    // Ensure configuration file exists on disk
    YamlConfigBootstrapper.EnsureConfigExists(configFileName, embeddedConfigResourceName);

    // Build host and register dependencies
    using var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddYamlFile(configFileName, optional: false, reloadOnChange: true);
            config.ExpandTemplates();
        })
        .ConfigureServices((context, services) =>
        {
            services.AddApplicationConfiguration(context.Configuration)
                .AddUserConfiguration()
                .AddMinecraftRegistries(context.Configuration)
                .AddDatapackServices()
                .AddWbAddonServices()
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