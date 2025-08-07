using Kjac.SearchProvider.Typesense.Configuration;
using Kjac.SearchProvider.Typesense.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Typesense;
using Umbraco.Cms.Core.Sync;

namespace Kjac.SearchProvider.Typesense.Tests;

[TestFixture]
public abstract class TypesenseTestBase
{
    private ServiceProvider _serviceProvider;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddTypesense(configuration)
            .AddLogging();

        serviceCollection.Configure<SearcherOptions>(
            options =>
            {
                options.MaxFacetValues = 500;
            }
        );

        serviceCollection.AddSingleton<IServerRoleAccessor, SingleServerRoleAccessor>();

        PerformAdditionalConfiguration(serviceCollection);

        _serviceProvider = serviceCollection.BuildServiceProvider();

        await PerformOneTimeSetUpAsync();
    }


    [OneTimeTearDown]
    public async Task TearDown()
    {
        await PerformOneTimeTearDownAsync();

        if (_serviceProvider is IDisposable disposableServiceProvider)
        {
            disposableServiceProvider.Dispose();
        }
    }

    protected virtual void PerformAdditionalConfiguration(ServiceCollection serviceCollection)
    {
    }

    protected virtual Task PerformOneTimeSetUpAsync()
        => Task.CompletedTask;

    protected virtual Task PerformOneTimeTearDownAsync()
        => Task.CompletedTask;

    protected T GetRequiredService<T>() where T : notnull
        => _serviceProvider.GetRequiredService<T>();

    protected async Task DeleteIndex(string indexAlias)
    {
        try
        {
            await GetRequiredService<ITypesenseClient>().DeleteCollection(indexAlias);
        }
        catch (TypesenseApiNotFoundException)
        {
            // the index does not exist
        }
    }
}
