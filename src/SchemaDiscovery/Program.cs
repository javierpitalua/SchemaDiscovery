using Autofac;
using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Core.Engine;
using System;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("SchemaDiscovery V1.0");
        Console.WriteLine("x Javier Pitalua. 2026.");

        var builder = new ContainerBuilder();
        builder.RegisterModule<SchemaDiscovery.Infrastructure.DefaultModule>();

        using (var container = builder.Build())
        {
            var extractor = container.Resolve<IExtractor>();
            extractor.ExtractAsync(new ExtractionOptions
            {
                ConnectionString = "Server=localhost;Database=AdventureWorks;User Id=sa;Password=Password123;",
                DatabaseType = DatabaseType.SqlServer.ToString(),
                OutputPath = "C:\\Projects\\schema-discovery\\output"
            }, default).Wait();
        }
    }
}