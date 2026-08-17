using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using SchemaDiscovery.Core.Abstractions;
using SchemaDiscovery.SqlServer;

namespace SchemaDiscovery.Infrastructure
{
    internal class DefaultModule : Autofac.Module 
    {
        override protected void Load(Autofac.ContainerBuilder builder)
        {
            builder
                    .RegisterAssemblyTypes(typeof(SchemaDiscovery.Core.AssemblyMarker).Assembly)
                    .AsImplementedInterfaces();
            
            
            builder.RegisterType<SqlServerCrawler>().As<IDatabaseCrawler>();
        }
    }
}
