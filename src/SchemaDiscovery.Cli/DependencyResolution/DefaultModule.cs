using Autofac;
using Microsoft.Extensions.Logging;
using SchemaDiscovery;
using SchemaDiscovery.Providers.MySql;
using SchemaDiscovery.Providers.PostgreSql;
using SchemaDiscovery.Providers.SqlServer;

namespace SchemaDiscovery.Cli.DependencyResolution
{
    public class DefaultModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(typeof(AssemblyMarker).Assembly)
                .AsImplementedInterfaces()
                .SingleInstance();

            // Built from the CommandLineOptions instance Program.cs registers before
            // building the container, so verbosity follows the parsed -v/--verbose flag.
            builder.Register(ctx =>
                {
                    var options = ctx.Resolve<CommandLineOptions>();
                    return LoggerFactory.Create(logging =>
                    {
                        logging
                            .AddSimpleConsole(c =>
                            {
                                c.SingleLine = true;
                                c.TimestampFormat = "HH:mm:ss ";
                            })
                            .SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information);
                    });
                })
                .As<ILoggerFactory>()
                .SingleInstance();

            // Lets constructors take ILogger<T> the same way Microsoft.Extensions.DependencyInjection
            // wires it, backed by the ILoggerFactory registered above.
            builder.RegisterGeneric(typeof(Logger<>))
                .As(typeof(ILogger<>))
                .SingleInstance();

            builder.RegisterType<SqlServerProviderFactory>().As<IDatabaseSchemaProviderFactory>().SingleInstance();
            builder.RegisterType<PostgreSqlProviderFactory>().As<IDatabaseSchemaProviderFactory>().SingleInstance();
            builder.RegisterType<MySqlProviderFactory>().As<IDatabaseSchemaProviderFactory>().SingleInstance();

            builder.RegisterType<ProviderFactory>().AsSelf().SingleInstance();
            builder.RegisterType<SchemaExportService>().AsSelf();

            base.Load(builder);
        }
    }
}
