﻿using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;

class Program
{
    static async Task Main()
    {
        Console.Title = "Samples.ServiceControl.ASBAdapter.Sales";
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVXYZ";
        var random = new Random();
        var endpointConfiguration = new EndpointConfiguration("Samples.ServiceControl.ASBAdapter.Sales");

#pragma warning disable 618
        var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();
#pragma warning restore 618
        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString.Sales");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("Could not read 'AzureServiceBus.ConnectionString.Sales' environment variable. Check sample prerequisites.");
        }
        
        transport.ConnectionString(connectionString);

        var routing = transport.Routing().ConnectToRouter("Router", true, false);
        routing.RouteToEndpoint(typeof(ShipOrder), "Samples.ServiceControl.ASBAdapter.Shipping");

        var recoverability = endpointConfiguration.Recoverability();

        endpointConfiguration.UsePersistence<InMemoryPersistence>();

        var chaos = new ChaosGenerator();
        endpointConfiguration.RegisterComponents(
            registration: components =>
            {
                components.ConfigureComponent(() => chaos, DependencyLifecycle.SingleInstance);
            });

        recoverability.Immediate(
            customizations: immediate =>
            {
                immediate.NumberOfRetries(0);
            });
        recoverability.Delayed(delayed => delayed.NumberOfRetries(0));

        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.EnableInstallers();
        endpointConfiguration.UseSerialization<NewtonsoftSerializer>();

        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press enter to exit");
        Console.WriteLine("Press 'o' to generate order");
        Console.WriteLine("Press 'f' to toggle simulating of message processing failure");
        while (true)
        {
            var key = Console.ReadKey();
            Console.WriteLine();
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }
            var lowerInvariant = char.ToLowerInvariant(key.KeyChar);
            if (lowerInvariant == 'o')
            {
                var orderId = new string(Enumerable.Range(0, 4).Select(x => letters[random.Next(letters.Length)]).ToArray());
                var shipOrder = new ShipOrder
                {
                    OrderId = orderId,
                    Value = random.Next(100)
                };
                var sendOptions = new SendOptions();
                await endpointInstance.Send(shipOrder, sendOptions)
                    .ConfigureAwait(false);
            }
            if (lowerInvariant == 'f')
            {
                chaos.IsFailing = !chaos.IsFailing;
                Console.WriteLine($"Failure simulation is now turned {(chaos.IsFailing ? "on" : "off")}");
                ConsoleHelper.ToggleTitle();
            }
        }
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }
}