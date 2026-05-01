using Kjac.SearchProvider.Typesense.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Models.Searching;

namespace Kjac.SearchProvider.Typesense.Tests;

public class TypesenseSearcherAccessContextTests : TypesenseTestBase
{
    private const string IndexAlias = "testindex";

    private readonly Dictionary<int, Guid> _documentIds = [];
    private readonly Guid _principalId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    protected override async Task PerformOneTimeSetUpAsync()
    {
        await EnsureIndex();

        ITypesenseIndexer indexer = GetRequiredService<ITypesenseIndexer>();

        for (var i = 1; i <= 100; i++)
        {
            var id = Guid.NewGuid();
            _documentIds[i] = id;

            await indexer.AddOrUpdateAsync(
                IndexAlias,
                id,
                UmbracoObjectTypes.Document,
                [new Variation(Culture: null, Segment: null)],
                [
                    new IndexField(
                        Umbraco.Cms.Search.Core.Constants.FieldNames.PathIds,
                        new IndexValue { Keywords = [id.AsKeyword()], },
                        Culture: null,
                        Segment: null
                    ),
                    new IndexField(
                        "theField",
                        new IndexValue { Texts = ["search"] },
                        Culture: null,
                        Segment: null
                    )
                ],
                i % 2 == 0
                    ? null
                    : i > 50
                        ? new ContentProtection([_groupId])
                        : new ContentProtection([_principalId])
            );
        }
    }

    protected override async Task PerformOneTimeTearDownAsync()
        => await DeleteIndex(IndexAlias);

    [Test]
    public async Task CanSearchAsAnonymous()
    {
        SearchResult result = await SearchAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(50));
            Assert.That(
                _documentIds.Where(d => d.Key % 2 == 0).Select(d => d.Value),
                Is.EquivalentTo(result.Documents.Select(d => d.Id)));
        });
    }

    [Test]
    public async Task CanSearchAsSpecificPrincipal()
    {
        SearchResult result = await SearchAsync(new AccessContext(_principalId, null));
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(75));
            Assert.That(
                _documentIds.Where(d => d.Key <= 50 || d.Key % 2 == 0).Select(d => d.Value),
                Is.EquivalentTo(result.Documents.Select(d => d.Id)));
        });
    }

    [Test]
    public async Task CanSearchAsGroup()
    {
        SearchResult result = await SearchAsync(new AccessContext(Guid.NewGuid(), [_groupId]));
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(75));
            Assert.That(
                _documentIds.Where(d => d.Key > 50 || d.Key % 2 == 0).Select(d => d.Value),
                Is.EquivalentTo(result.Documents.Select(d => d.Id)));
        });
    }

    [Test]
    public async Task CanBypassProtection()
    {
        SearchResult result = await SearchAsync(AccessContext.BypassProtection());
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(100));
            Assert.That(_documentIds.Values, Is.EquivalentTo(result.Documents.Select(d => d.Id)));
        });
    }

    private async Task<SearchResult> SearchAsync(AccessContext? accessContext)
    {
        ITypesenseSearcher searcher = GetRequiredService<ITypesenseSearcher>();
        SearchResult result = await searcher.SearchAsync(IndexAlias, query: "search", accessContext: accessContext, take: 100);

        Assert.That(result, Is.Not.Null);
        return result;
    }

    private async Task EnsureIndex()
    {
        await DeleteIndex(IndexAlias);

        await GetRequiredService<ITypesenseIndexManager>().EnsureAsync(IndexAlias);
    }
}
