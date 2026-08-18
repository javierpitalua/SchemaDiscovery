using Microsoft.Extensions.Logging;
using SchemaDiscovery.Cli;

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

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddSimpleConsole(c =>
        {
            c.SingleLine = true;
            c.TimestampFormat = "HH:mm:ss ";
        })
        .SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information);
});

var logger = loggerFactory.CreateLogger("SchemaDiscovery");
var providerFactory = new ProviderFactory();

try
{
    logger.LogInformation("Starting schema scan using provider '{Provider}'.", options.Provider);

    await using var provider = providerFactory.Create(options.Provider, options.ConnectionString, loggerFactory);

    var exportService = new SchemaExportService(loggerFactory.CreateLogger<SchemaExportService>());
    var outputPath = Path.GetFullPath(options.OutputDirectory);

    var count = await exportService.ExportAsync(
        provider,
        outputPath,
        options.SkipViews,
        options.SkipProcedures,
        options.SkipFunctions,
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
