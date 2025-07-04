using Kjac.SearchProvider.Typesense.Configuration;
using Kjac.SearchProvider.Typesense.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Typesense.Setup;
using Umbraco.Cms.Search.Core.Services;

namespace Kjac.SearchProvider.Typesense.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTypesense(this IServiceCollection services, IConfiguration configuration)
    {
        // register the Typesense searcher and indexer so they can be used explicitly for index registrations
        services.AddTransient<ITypesenseIndexer, TypesenseIndexer>();
        services.AddTransient<ITypesenseSearcher, TypesenseSearcher>();

        // register the Typesense searcher and indexer as the defaults
        services.AddTransient<IIndexer, TypesenseIndexer>();
        services.AddTransient<ISearcher, TypesenseSearcher>();

        // register supporting services
        services.AddSingleton<ITypesenseIndexManager, TypesenseIndexManager>();

        var clientOptions = new ClientOptions();
        IConfigurationSection clientConfiguration = configuration.GetSection("TypesenseSearchProvider:Client");
        if (clientConfiguration.Exists())
        {
            clientConfiguration.Bind(clientOptions);
        }

        if (clientOptions.Host?.IsAbsoluteUri is not true)
        {
            Console.WriteLine("ERROR: The Typesense search provider configuration is either missing or invalid.");
        }

        services.AddTypesenseClient(
            config =>
            {
                config.ApiKey = clientOptions.Authentication?.ApiKey ?? string.Empty;
                config.Nodes = clientOptions.Host is not null
                    ? [new Node(clientOptions.Host.Host, clientOptions.Host.Port.ToString(), clientOptions.Host.Scheme)]
                    : [];
            },
            enableHttpCompression: false
        );

        return services;
    }
}
