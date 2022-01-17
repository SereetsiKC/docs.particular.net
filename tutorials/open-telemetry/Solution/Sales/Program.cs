﻿using System;
using System.Threading.Tasks;
using NServiceBus;
using System.Data.SqlClient;

namespace Sales
{
    using Azure.Monitor.OpenTelemetry.Exporter;
    using Honeycomb.OpenTelemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using System.Diagnostics;

    class Program
    {
        static void Main(string[] args)
        {
            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                ActivityStopped = activity =>
                {
                    foreach (var (key, value) in activity.Baggage)
                    {
                        activity.AddTag(key, value);
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);

            CreateHostBuilder(args).Build().Run();
            Console.Title = EndpointName;
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseNServiceBus(hostBuilderContext =>
                {
                    var endpointConfiguration = new EndpointConfiguration(EndpointName);

                    var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();
                    transport.ConnectionString("enter-connectionstring");

                    var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
                    persistence.ConnectionBuilder(() => new SqlConnection("enter-connectionstring"));
                    persistence.SqlDialect<SqlDialect.MsSqlServer>();

                    endpointConfiguration.EnableInstallers();

                    return endpointConfiguration;
                })
                .ConfigureServices((_, services) =>
                {
                    AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
                    services.AddOpenTelemetryTracing(builder => builder
                                                                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(EndpointName))
                                                                .AddSqlClientInstrumentation()
                                                                .AddSource("NServiceBus")
                                                                .AddSource("Azure.*")
                                                                .AddJaegerExporter(c =>
                                                                {
                                                                    c.AgentHost = "localhost";
                                                                    c.AgentPort = 6831;
                                                                })
                                                                // .AddAzureMonitorTraceExporter(c =>
                                                                // {
                                                                //     c.ConnectionString = "enter-instrumentationconnectionstring;
                                                                // })
                    );
                });

        public static string EndpointName => "Sales";
    }
}