using Kjac.SearchProvider.Typesense.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;

namespace Kjac.SearchProvider.Typesense.Tests;

public partial class TypesenseSearcherVarianceTests : TypesenseTestBase
{
    private const string IndexAlias = "varianceindex";
    private const string FieldInvariance = "FieldOne";
    private const string FieldCultureVariance = "FieldTwo";
    private const string FieldMixedVariance = "FieldThree";

    private readonly Dictionary<int, Guid> _documentIds = [];

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
                [
                    new Variation(Culture: "en-US", Segment: null),
                    new Variation(Culture: "da-DK", Segment: null),
                ],
                [
                    new IndexField(
                        Umbraco.Cms.Search.Core.Constants.FieldNames.PathIds,
                        new IndexValue { Keywords = [id.AsKeyword()], },
                        Culture: null,
                        Segment: null
                    ),
                    new IndexField(
                        FieldInvariance,
                        new IndexValue
                        {
                            Texts = ["invariant", $"invariant{i}"]
                        },
                        Culture: null,
                        Segment: null
                    ),
                    new IndexField(
                        FieldCultureVariance,
                        new IndexValue
                        {
                            Texts = ["english", $"english{i}"]
                        },
                        Culture: "en-US",
                        Segment: null
                    ),
                    new IndexField(
                        FieldCultureVariance,
                        new IndexValue
                        {
                            Texts = ["danish", $"danish{i}"]
                        },
                        Culture: "da-DK",
                        Segment: null
                    ),
                    new IndexField(
                        FieldMixedVariance,
                        new IndexValue
                        {
                            Texts = ["mixedinvariant", $"mixedinvariant{i}"]
                        },
                        Culture: null,
                        Segment: null
                    ),
                    new IndexField(
                        FieldMixedVariance,
                        new IndexValue
                        {
                            Texts = ["mixedenglish",  $"mixedenglish{i}"]
                        },
                        Culture: "en-US",
                        Segment: null
                    ),
                    new IndexField(
                        FieldMixedVariance,
                        new IndexValue
                        {
                            Texts = ["mixeddanish",   $"mixeddanish{i}"]
                        },
                        Culture: "da-DK",
                        Segment: null
                    ),
                ],
                null
            );
        }
    }

    protected override async Task PerformOneTimeTearDownAsync()
        => await DeleteIndex(IndexAlias);

    private async Task<SearchResult> SearchAsync(
        string? query = null,
        IEnumerable<Filter>? filters = null,
        IEnumerable<Facet>? facets = null,
        IEnumerable<Sorter>? sorters = null,
        string? culture = null,
        string? segment = null,
        AccessContext? accessContext = null,
        int skip = 0,
        int take = 100)
    {
        ITypesenseSearcher searcher = GetRequiredService<ITypesenseSearcher>();
        SearchResult result = await searcher.SearchAsync(
            IndexAlias,
            query,
            filters,
            facets,
            sorters,
            culture,
            segment,
            accessContext,
            skip,
            take
        );

        Assert.That(result, Is.Not.Null);
        return result;
    }

    private async Task EnsureIndex()
    {
        await DeleteIndex(IndexAlias);

        await GetRequiredService<ITypesenseIndexManager>().EnsureAsync(IndexAlias);
    }
}
