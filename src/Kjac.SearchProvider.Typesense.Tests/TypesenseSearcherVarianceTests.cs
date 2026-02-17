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
    private const string FieldSegmentVariance = "FieldFour";

    private readonly Dictionary<int, Guid> _variantDocumentIds = [];
    private readonly Dictionary<int, Guid> _invariantDocumentIds = [];

    protected override async Task PerformOneTimeSetUpAsync()
    {
        await EnsureIndex();

        ITypesenseIndexer indexer = GetRequiredService<ITypesenseIndexer>();

        for (var i = 1; i <= 100; i++)
        {
            var id = Guid.NewGuid();
            _variantDocumentIds[i] = id;

            await indexer.AddOrUpdateAsync(
                IndexAlias,
                id,
                UmbracoObjectTypes.Document,
                [
                    new Variation(Culture: "en-US", Segment: null),
                    new Variation(Culture: "da-DK", Segment: null),
                    new Variation(Culture: "en-US", Segment: "seg1"),
                    new Variation(Culture: "da-DK", Segment: "seg1"),
                    new Variation(Culture: "en-US", Segment: "seg2"),
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
                            Texts = ["invariant", $"invariant{i}", "commoninvariant"]
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
                    new IndexField(
                        FieldSegmentVariance,
                        new IndexValue
                        {
                            Texts = ["defaultenglish",   $"defaultenglish{i}"],
                            Keywords = ["defaultkeywordenglish",   $"defaultkeywordenglish{i}"],
                            Integers = [i],
                            Decimals = [i],
                            DateTimeOffsets = [StartDate().AddDays(i)]
                        },
                        Culture: "en-US",
                        Segment: null
                    ),
                    new IndexField(
                        FieldSegmentVariance,
                        new IndexValue
                        {
                            Texts = ["seg1english",   $"seg1english{i}"],
                            Keywords = ["seg1keywordenglish",   $"seg1keywordenglish{i}"],
                            Integers = [100 + i],
                            Decimals = [100 + i],
                            DateTimeOffsets = [StartDate().AddDays(100 + i)]
                        },
                        Culture: "en-US",
                        Segment: "seg1"
                    ),
                    new IndexField(
                        FieldSegmentVariance,
                        new IndexValue
                        {
                            Texts = ["defaultdanish",   $"defaultdanish{i}"],
                            Keywords = ["defaultkeyworddanish",   $"defaultkeyworddanish{i}"],
                            Integers = [200 + i],
                            Decimals = [200 + i],
                            DateTimeOffsets = [StartDate().AddDays(200 + i)]
                        },
                        Culture: "da-DK",
                        Segment: null
                    ),
                    new IndexField(
                        FieldSegmentVariance,
                        new IndexValue
                        {
                            Texts = ["seg1danish",   $"seg1danish{i}"],
                            Keywords = ["seg1keyworddanish",   $"seg1keyworddanish{i}"],
                            Integers = [300 + i],
                            Decimals = [300 + i],
                            DateTimeOffsets = [StartDate().AddDays(300 + i)]
                        },
                        Culture: "da-DK",
                        Segment: "seg1"
                    ),
                    new IndexField(
                        FieldSegmentVariance,
                        new IndexValue
                        {
                            Texts = ["seg2english",   $"seg2english{i}"],
                            Keywords = ["seg2keywordenglish",   $"seg2keywordenglish{i}"],
                            Integers = [400 + i],
                            Decimals = [400 + i],
                            DateTimeOffsets = [StartDate().AddDays(400 + i)]
                        },
                        Culture: "en-US",
                        Segment: "seg2"
                    ),
                ],
                null
            );

            id = Guid.NewGuid();
            _invariantDocumentIds[i] = id;

            await indexer.AddOrUpdateAsync(
                IndexAlias,
                id,
                UmbracoObjectTypes.Document,
                [
                    new Variation(Culture: null, Segment: null)
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
                            Texts = ["commoninvariant", $"commoninvariant{i}"]
                        },
                        Culture: null,
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

    private DateTimeOffset StartDate()
        => Date(2025, 01, 01);

    private DateTimeOffset Date(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        => new(year, month, day, hour, minute, second, TimeSpan.Zero);
}
