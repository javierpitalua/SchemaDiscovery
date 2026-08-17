using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.Core.Engine;

namespace SchemaDiscovery.Infrastructure;

public static class OptionsParser
{
    /// <summary>Returns null when --help was requested (usage has already been printed).</summary>
    public static ExtractionOptions? ParseOptions(string[] args)
    {
        string? connectionString = null;
        string? databaseType = null;
        string? outputPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    PrintHelp();
                    return null;
                case "--connection-string":
                case "-c":
                    connectionString = RequireValue(args, ref i, args[i]);
                    break;
                case "--database-type":
                case "-d":
                    databaseType = RequireValue(args, ref i, args[i]);
                    break;
                case "--output-path":
                case "-o":
                    outputPath = RequireValue(args, ref i, args[i]);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}. Run with --help for usage.");
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("--connection-string is required. Run with --help for usage.");
        }

        return new ExtractionOptions
        {
            ConnectionString = connectionString,
            DatabaseType = string.IsNullOrWhiteSpace(databaseType)
                ? DatabaseType.SqlServer
                : ResolveDatabaseType(databaseType),
            OutputPath = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "schema-files")
                : outputPath
        };
    }

    private static DatabaseType ResolveDatabaseType(string input)
    {
        if (Enum.TryParse<DatabaseType>(input, out DatabaseType result))
        {
            return result;
        }
        else
        {
            return DatabaseType.undefined;
        }
    }

    private static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {flag}.");
        }

        return args[++index];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
                          SchemaDiscovery - extracts database schema metadata (tables, views, stored procedures) to disk.

                          Usage:
                            SchemaDiscovery --connection-string <value> [options]

                          Options:
                            -c, --connection-string <value>  Connection string for the target database. Required.
                            -d, --database-type <value>      Database provider to crawl: SqlServer, MySql, PostgreSql, or Oracle.
                                                              Defaults to SqlServer if not specified.
                            -o, --output-path <path>         Directory where extracted schema files are written.
                                                              Defaults to '<current-directory>/schema-files' if not specified.
                            -h, --help                       Show this help message and exit.

                          Example:
                            SchemaDiscovery -c "Server=localhost;Database=AdventureWorks;User Id=sa;Password=***;" -d SqlServer -o C:\out
                          """);
    }
}