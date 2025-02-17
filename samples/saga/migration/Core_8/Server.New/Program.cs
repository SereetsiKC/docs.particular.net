﻿//#define POST_MIGRATION

using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NServiceBus;

class Program
{
    static async Task Main()
    {
        Console.Title = "Samples.SagaMigration.Server.New";
        var endpointConfiguration = new EndpointConfiguration("Samples.SagaMigration.Server");
        endpointConfiguration.EnableInstallers();

#if !POST_MIGRATION
        endpointConfiguration.OverrideLocalAddress("Samples.SagaMigration.Server.New");
#endif

        endpointConfiguration.UseTransport(new LearningTransport());
        var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
        persistence.SqlDialect<SqlDialect.MsSqlServer>();
        var connection = @"Data Source=.\SqlExpress;Initial Catalog=NsbSamplesSagaMigration;Integrated Security=True;";
        persistence.ConnectionBuilder(
            connectionBuilder: () =>
            {
                return new SqlConnection(connection);
            });
        persistence.TablePrefix("New");

        SqlHelper.EnsureDatabaseExists(connection);
        var endpoint = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);

        Console.WriteLine("Press <enter> to exit.");
        Console.ReadLine();

        await endpoint.Stop()
            .ConfigureAwait(false);
    }
}
