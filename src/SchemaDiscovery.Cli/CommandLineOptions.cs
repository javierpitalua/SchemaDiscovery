namespace SchemaDiscovery.Cli;

public sealed class CommandLineOptions
{
    public string ConnectionString { get; private set; } = string.Empty;
    public string Provider { get; private set; } = "sqlserver";
    public string OutputDirectory { get; private set; } = "./schema-output";
    public bool Verbose { get; private set; }
    public bool SkipViews { get; private set; }
    public bool SkipProcedures { get; private set; }
    public bool SkipFunctions { get; private set; }
    public bool ShowHelp { get; private set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-c":
                case "--connection-string":
                    options.ConnectionString = RequireValue(args, ref i, arg);
                    break;

                case "-p":
                case "--provider":
                    options.Provider = RequireValue(args, ref i, arg).ToLowerInvariant();
                    break;

                case "-o":
                case "--output":
                    options.OutputDirectory = RequireValue(args, ref i, arg);
                    break;

                case "-v":
                case "--verbose":
                    options.Verbose = true;
                    break;

                case "--skip-views":
                    options.SkipViews = true;
                    break;

                case "--skip-procedures":
                    options.SkipProcedures = true;
                    break;

                case "--skip-functions":
                    options.SkipFunctions = true;
                    break;

                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int i, string argName)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Missing value for argument '{argName}'.");

        i++;
        return args[i];
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            schema-discovery - scans a database's schema and exports each object as a JSON file.

            USAGE:
              schema-discovery -c "<connection string>" [-p sqlserver|postgres|mysql] [-o ./output] [options]

            OPTIONS:
              -c, --connection-string <value>   Database connection string (required)
              -p, --provider <value>            Database provider. Default: sqlserver
              -o, --output <path>               Output folder for JSON files. Default: ./schema-output
              -v, --verbose                     Enable verbose (debug) logging
                  --skip-views                  Do not export views
                  --skip-procedures             Do not export stored procedures
                  --skip-functions              Do not export functions
              -h, --help                        Show this help message

            EXAMPLES:
              schema-discovery -c "Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True;" -o ./out
              schema-discovery -c "Server=.;Database=MyDb;User Id=sa;Password=***;" -p sqlserver -v
            """);
    }
}
