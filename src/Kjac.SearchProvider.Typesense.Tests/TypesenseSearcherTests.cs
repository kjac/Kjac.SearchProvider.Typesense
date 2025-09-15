using Kjac.SearchProvider.Typesense.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;

namespace Kjac.SearchProvider.Typesense.Tests;

public partial class TypesenseSearcherTests : TypesenseTestBase
{
    private const string IndexAlias = "testindex";
    private const string FieldMultipleValues = "FieldOne";
    private const string FieldSingleValue = "FieldTwo";
    private const string FieldMultiSorting = "FieldThree";
    private const string FieldTextRelevance = "FieldFour";
    private const string FieldTextSorting = "FieldFive";

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
                i <= 25
                    ? UmbracoObjectTypes.Document
                    : i <= 50
                        ? UmbracoObjectTypes.Media
                        : i <= 75
                            ? UmbracoObjectTypes.Member
                            : UmbracoObjectTypes.Unknown,
                [new Variation(Culture: null, Segment: null)],
                [
                    new IndexField(
                        Umbraco.Cms.Search.Core.Constants.FieldNames.PathIds,
                        new IndexValue { Keywords = [id.AsKeyword()], },
                        Culture: null,
                        Segment: null
                    ),
                    new IndexField(
                        FieldMultipleValues,
                        new IndexValue
                        {
                            Decimals = [i, i * 1.5m, i * -1m, i * -1.5m],
                            Integers = [i, i * 10, i * -1, i * -10],
                            Keywords = ["all", i % 2 == 0 ? "even" : "odd", $"single{i}"],
                            DateTimeOffsets =
                            [
                                StartDate().AddDays(i),
                                StartDate(),
                                StartDate().AddDays(i * 2)
                            ],
                            Texts = ["all", i % 2 == 0 ? "even" : "odd", $"single{i}", $"phrase search single{i}"]
                        },
                        Culture: null,
                        Segment: null
                    ),
                    new IndexField(
                        FieldSingleValue,
                        new IndexValue
                        {
                            Decimals = [i * 0.01m],
                            Integers = [i],
                            Keywords = [$"single{i}"],
                            DateTimeOffsets = [StartDate().AddDays(i)],
                            Texts = [$"single{i}"]
                        },
                        Culture: null,
                        Segment: null
                    ),
                    new IndexField(
                        FieldMultiSorting,
                        new IndexValue
                        {
                            Decimals = [i % 2 == 0 ? 10m : 20m],
                            Integers = [i % 2 == 0 ? 10 : 20],
                            Keywords = [i % 2 == 0 ? "even" : "odd"],
                            DateTimeOffsets = [i % 2 == 0 ? StartDate().AddDays(1) : StartDate().AddDays(2)],
                            Texts = [i == 40 ? "sortable_d" : "sortable_x"],
                            TextsR1 = [i == 10 ? "sortable_a" : "sortable_x"],
                            TextsR2 = [i == 20 ? "sortable_b" : "sortable_x"],
                            TextsR3 = [i == 30 ? "sortable_c" : "sortable_x"]
                        },
                        Culture: null,
                        Segment: null
                    ),
                    new IndexField(
                        FieldTextRelevance,
                        new IndexValue
                        {
                            Texts = [$"texts_{i}", i == 10 ? "special" : "common"],
                            TextsR1 = [$"texts_r1_{i}", i == 30 ? "special" : "common"],
                            TextsR2 = [$"texts_r2_{i}", i == 20 ? "special" : "common"],
                            TextsR3 = [$"texts_r3_{i}", i == 40 ? "special" : "common"]
                        },
                        Culture: null,
                        Segment: null
                    ),
                    new IndexField(
                        FieldTextSorting,
                        new IndexValue
                        {
                            Texts = ["xxx"],
                            TextsR1 = i == 60 ? ["aaa", "BBB", "ccc"] : null,
                            TextsR2 = i == 20 ? ["BBB", "ccc"] : null,
                            TextsR3 = i == 40 ? ["ccc"] : null
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

    private int[] OddOrEvenIds(bool even)
        => Enumerable
            .Range(1, 50)
            .Select(i => i * 2)
            .Select(i => even ? i : i - 1)
            .ToArray();
}
