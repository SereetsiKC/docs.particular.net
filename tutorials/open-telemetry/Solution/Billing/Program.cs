﻿using System;
using NServiceBus;
using OpenTelemetry.Logs;

namespace Billing
{
    using Azure.Monitor.OpenTelemetry.Exporter;
    using Honeycomb.OpenTelemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
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

                    var transport = endpointConfiguration.UseTransport<LearningTransport>();
                    var persistence = endpointConfiguration.UsePersistence<LearningPersistence>();

                    endpointConfiguration.RegisterComponents(
                        c =>
                        {
                            c.AddScoped<OrderCalculator>();
                        }
                    );

                    return endpointConfiguration;
                })
                .ConfigureServices((_, services) =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddApplicationInsights(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY"));
                        builder.AddOpenTelemetry(o => o.AddConsoleExporter());
                    });
                    services.AddOpenTelemetryTracing(builder => builder
                                                                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(EndpointName))
                                                                .AddSource("NServiceBus")
                                                                .AddSource("Test")
                                                                .AddJaegerExporter(c =>
                                                                {
                                                                    c.AgentHost = "localhost";
                                                                    c.AgentPort = 6831;
                                                                })
                                                                .AddAzureMonitorTraceExporter(c => { c.ConnectionString = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY"); })
                                                                .AddHoneycomb(new HoneycombOptions
                                                                {
                                                                    ApiKey = Environment.GetEnvironmentVariable("HONEYCOMB_APIKEY"),
                                                                    Dataset = "spike-core"
                                                                })
                    );
                    services.AddHostedService<TestService>();
                });
        public static string EndpointName => "Billing";
    }
}
