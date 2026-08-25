using Autofac;
using Microsoft.Extensions.Logging;
using SchemaDiscovery;
using SchemaDiscovery.Cli;
using SchemaDiscovery.Cli.DependencyResolution;

CommandLineOptions options;
try
{
    options = CommandLineOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.WriteLine();
    CommandLineOptions.PrintHelp();
    return 1;
}

if (options.ShowHelp || args.Length == 0)
{
    CommandLineOptions.PrintHelp();
    return 0;
}

if (string.IsNullOrWhiteSpace(options.ConnectionString))
{
    Console.Error.WriteLine("Error: --connection-string is required.");
    Console.WriteLine();
    CommandLineOptions.PrintHelp();
    return 1;
}

var containerBuilder = new ContainerBuilder();
containerBuilder.RegisterInstance(options);
containerBuilder.RegisterModule<DefaultModule>();

using var container = containerBuilder.Build();

var loggerFactory = container.Resolve<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("SchemaDiscovery");
var providerFactory = container.Resolve<ProviderFactory>();

try
{
    logger.LogInformation("Starting schema scan using provider '{Provider}'.", options.Provider);

    var culture = options.Language switch
    {
        "en" => CultureLanguages.English,
        "es" => CultureLanguages.Spanish,
        _ => throw new NotSupportedException($"Unsupported language '{options.Language}'.")
    };

    await using var provider = providerFactory.Create(options.Provider, options.ConnectionString, loggerFactory, culture);

    var exportService = container.Resolve<SchemaExportService>();
    var outputPath = Path.GetFullPath(options.OutputDirectory);

    var count = await exportService.ExportAsync(
        provider,
        outputPath,
        options.SkipViews,
        options.SkipProcedures,
        options.SkipFunctions,
        options.Language,
        CancellationToken.None);

    logger.LogInformation("Done. Exported {Count} object(s) to '{Output}'.", count, outputPath);
    return 0;
}
catch (NotSupportedException ex)
{
    logger.LogError("{Message}", ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Supported providers: {string.Join(", ", providerFactory.SupportedProviders)}");
    return 1;
}
catch (Exception ex)
{
    logger.LogError(ex, "Schema scan failed.");
    return 1;
}
