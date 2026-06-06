using EventDbLite.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.MongoDb.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDbSnapshotStorage(this IServiceCollection services,string connectionStringKey = "MongoDb")
    {
        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(connectionStringKey);

            return new MongoClient(connectionString);
        });

        services.TryAddTransient<ISnapshotRepository, MongoDbSnapshotRepository>();

        return services;
    }
}
