using Autofac;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.Cli.DependencyResolution
{
    public class DefaultModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(typeof(AssemblyMarker).Assembly)
                .AsImplementedInterfaces()
                .SingleInstance();



            base.Load(builder);
        }
    }
}
