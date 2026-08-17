using Autofac;
using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Core.Engine;
using Serilog;
using System;
using System.IO;
using SchemaDiscovery.Infrastructure;

internal class Program
{
    private static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("SchemaDiscovery V1.0");
            Log.Information("x Javier Pitalua. 2026.");

            ExtractionOptions? options;
            try
            {
                options = OptionsParser.ParseOptions(args);
            }
            catch (ArgumentException ex)
            {
                Log.Error(ex.Message);
                return;
            }

            if (options is null)
            {
                return;
            }

            var builder = new ContainerBuilder();
            builder.RegisterInstance(Log.Logger).As<ILogger>();
            builder.RegisterModule<SchemaDiscovery.Infrastructure.DefaultModule>();

            using (var container = builder.Build())
            {
                var extractor = container.Resolve<IExtractor>();
                extractor.ExtractAsync(options, default).Wait();
            }
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}