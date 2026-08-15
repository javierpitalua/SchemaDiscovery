using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.Infrastructure
{
    internal class DefaultModule : Autofac.Module 
    {
        override protected void Load(Autofac.ContainerBuilder builder)
        {
         //   builder.RegisterType<SqlServer.SqlServerCrawler>().As<IDatabaseCrawler>();
        }
    }
}
