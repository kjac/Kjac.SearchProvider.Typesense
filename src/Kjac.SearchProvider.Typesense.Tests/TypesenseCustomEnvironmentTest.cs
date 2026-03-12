using Kjac.SearchProvider.Typesense.Configuration;
using Kjac.SearchProvider.Typesense.Services;
using Microsoft.Extensions.DependencyInjection;
using Typesense;

namespace Kjac.SearchProvider.Typesense.Tests;

public class TypesenseCustomEnvironmentTest : TypesenseTestBase
{
    private const string IndexAlias = "someindex";
    private const string Environment = "test";

    protected override void PerformAdditionalConfiguration(ServiceCollection serviceCollection)
        => serviceCollection.Configure<ClientOptions>(
            options =>
            {
                options.Environment = Environment;
            }
        );

    [SetUp]
    [TearDown]
    protected async Task CleanUp()
        => await DeleteIndex(EnvironmentIndexAlias());

    [Test]
    public async Task CanCreateCustomEnvironmentIndex()
    {
        ITypesenseClient client = GetRequiredService<ITypesenseClient>();

        await IndexManager.EnsureAsync(IndexAlias);

        CollectionResponse collectionResponse = await client.RetrieveCollection(EnvironmentIndexAlias());
        Assert.That(collectionResponse.Name, Is.EqualTo(EnvironmentIndexAlias()));
    }

    [Test]
    public async Task CanResetCustomEnvironmentIndex()
    {
        ITypesenseClient client = GetRequiredService<ITypesenseClient>();

        await IndexManager.ResetAsync(IndexAlias);

        CollectionResponse collectionResponse = await client.RetrieveCollection(EnvironmentIndexAlias());
        Assert.That(collectionResponse.Name, Is.EqualTo(EnvironmentIndexAlias()));
    }

    private ITypesenseIndexManager IndexManager => GetRequiredService<ITypesenseIndexManager>();

    private string EnvironmentIndexAlias() => $"{IndexAlias}_{Environment}";

}
