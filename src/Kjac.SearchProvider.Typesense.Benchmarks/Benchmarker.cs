using Bogus;
using Kjac.SearchProvider.Typesense.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;

public class Benchmarker
{
    private const string IndexAlias = "benchmarkindex";

    private readonly string[] _facetGroup1 = ["sint", "voluptatem", "quis", "quod", "quam", "inventore", "voluptatum", "distinctio", "eius", "vitae"];
    private readonly string[] _facetGroup2 = ["omnis", "error", "consequatur", "culpa", "quia", "quibusdam", "voluptas", "possimus", "commodi", "dolor"];
    private readonly string[] _facetGroup3 = ["cupiditate", "vero", "excepturi", "officia", "rerum", "quasi", "asperiores", "libero", "iure", "enim"];
    private readonly string[] _facetGroup4 = ["illo", "beatae", "accusamus", "soluta", "magni", "dolorum", "repellendus", "eaque", "adipisci", "ullam"];
    private readonly string[] _facetGroup5 = ["aperiam", "laborum", "consectetur", "ornare", "pretium", "eleifend", "commodo", "nunc", "amet", "massa"];
    private readonly Random _randomizer = new();

    private readonly ITypesenseIndexer _indexer;
    private readonly ITypesenseSearcher  _searcher;

    private List<string> _searchTerms = new();

    public Benchmarker(ITypesenseIndexer indexer, ITypesenseSearcher searcher)
    {
        _indexer = indexer;
        _searcher = searcher;
    }

    public async Task RunAsync()
    {
        await SeedDatabase();

        await WarmUp();

        await BenchmarkSingleFacetSingleValue();
        await BenchmarkSingleFacetMultiValue();
        await BenchmarkMultiFacetSingleValue();
        await BenchmarkMultiFacetMultiValue();
        await BenchmarkSingleFacetSingleValueWithSearchTerm();
        await BenchmarkSingleFacetSingleValueInParallel();
        await BenchmarkSearchTerm();
    }

    private async Task WarmUp() => await PerformSingleFacetSearch(1, 1, []);

    private async Task BenchmarkSingleFacetSingleValue()
        => await PerformBenchmark(
            nameof(BenchmarkSingleFacetSingleValue),
            async (count, timings) => await PerformSingleFacetSearch(count, 1, timings)
        );

    private async Task BenchmarkSingleFacetMultiValue()
        => await PerformBenchmark(
            nameof(BenchmarkSingleFacetMultiValue),
            async (count, timings) => await PerformSingleFacetSearch(count, 3, timings)
        );

    private async Task BenchmarkMultiFacetSingleValue()
        => await PerformBenchmark(
            nameof(BenchmarkMultiFacetSingleValue),
            async (count, timings) => await PerformMultiFacetSearch(count, 1, timings)
        );

    private async Task BenchmarkMultiFacetMultiValue()
        => await PerformBenchmark(
            nameof(BenchmarkMultiFacetMultiValue),
            async (count, timings) => await PerformMultiFacetSearch(count, 3, timings)
        );

    private async Task BenchmarkSingleFacetSingleValueWithSearchTerm()
        => await PerformBenchmark(
            nameof(BenchmarkSingleFacetSingleValueWithSearchTerm),
            async (count, timings) => await PerformSingleFacetSearch(count, 1, timings, RandomSearchTerm())
        );

    private async Task BenchmarkSingleFacetSingleValueInParallel()
    {
        Console.WriteLine();
        Console.WriteLine($"Benchmark: {nameof(BenchmarkSingleFacetSingleValueInParallel)}...");
        Console.Write("> ");

        var timings = new List<TimeSpan>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 1000),
            new ParallelOptions { MaxDegreeOfParallelism = 10 },
            async (count, _) =>
            {
                if (count % 20 == 0)
                {
                    Console.Write("=");
                }

                await PerformSingleFacetSearch(count, 1, timings);
            }
        );

        Console.WriteLine(" done!");
        Console.WriteLine();

        PrintResults(timings);
    }

    private async Task BenchmarkSearchTerm()
        => await PerformBenchmark(
            nameof(BenchmarkSearchTerm),
            async (count, timings) =>
            {
                var start = DateTimeOffset.UtcNow;

                var cultureDiscriminator = count % 3;
                var culture = cultureDiscriminator == 1 ? "en-US" : cultureDiscriminator == 2 ? "da-DK" : null;

                var result = await _searcher.SearchAsync(
                    IndexAlias,
                    culture: culture,
                    query: RandomSearchTerm()
                );

                timings.Add(DateTimeOffset.UtcNow - start);
            });

    private async Task PerformBenchmark(string name, Func<int, List<TimeSpan>, Task> action)
    {
        Console.WriteLine();
        Console.WriteLine($"Benchmark: {name}...");
        Console.Write("> ");

        var timings = new List<TimeSpan>();

        foreach (var count in Enumerable.Range(0, 100))
        {
            if (count % 20 == 0)
            {
                Console.Write("=");
            }

            await action(count, timings);
        }

        Console.WriteLine(" done!");
        Console.WriteLine();

        PrintResults(timings);
    }

    private async Task SeedDatabase()
    {
        await _indexer.ResetAsync(IndexAlias);

        Console.WriteLine();
        Console.WriteLine("Seeding the test data...");
        Console.Write("> ");

        _searchTerms.Clear();

        var faker = new Faker();
        for (var i = 0; i < 1000; i++)
        {
            if (i % 20 == 0)
            {
                Console.Write("=");
            }

            var id = Guid.NewGuid();
            var title = faker.Commerce.ProductName();
            var body = faker.Lorem.Paragraph();
            _searchTerms.AddRange((title + body).Split(' ').Where(IsValidSearchTerm));

            var randomInt = _randomizer.Next(0, 5);
            var culture = randomInt <= 2 ? null : randomInt == 3 ? "en-US" : "da-DK";

            await _indexer.AddOrUpdateAsync(
                IndexAlias,
                id,
                i <= 25
                    ? UmbracoObjectTypes.Document
                    : i <= 50
                        ? UmbracoObjectTypes.Media
                        : i <= 75
                            ? UmbracoObjectTypes.Member
                            : UmbracoObjectTypes.Unknown,
                [new Variation(Culture: culture, Segment: null)],
                [
                    new IndexField(
                        Umbraco.Cms.Search.Core.Constants.FieldNames.PathIds,
                        new IndexValue { Keywords = [id.AsKeyword()] },
                        Culture: null,
                        Segment: null
                    ),
                    // create random facet values from all facet groups
                    new(
                        "facetOne",
                        new IndexValue { Keywords = Randomize(_facetGroup1, 2) },
                        Culture: null,
                        Segment: null
                    ),
                    new(
                        "facetTwo",
                        new IndexValue { Keywords = Randomize(_facetGroup2, 2) },
                        Culture: null,
                        Segment: null
                    ),
                    new(
                        "facetThree",
                        new IndexValue { Keywords = Randomize(_facetGroup3, 2) },
                        Culture: null,
                        Segment: null
                    ),
                    new(
                        "facetFour",
                        new IndexValue { Keywords = Randomize(_facetGroup4, 2) },
                        Culture: null,
                        Segment: null
                    ),
                    new(
                        "facetFive",
                        new IndexValue { Keywords = Randomize(_facetGroup5, 2) },
                        Culture: null,
                        Segment: null
                    ),
                    // add a couple of text fields for full text search
                    new(
                        "title",
                        new IndexValue { Texts = [title] },
                        Culture: null,
                        Segment: null
                    ),
                    new(
                        "body",
                        new IndexValue { Texts = [body] },
                        Culture: null,
                        Segment: null
                    ),
                ],
                null
            );
        }
        _searchTerms = _searchTerms.Distinct().Take(100).ToList();

        Console.WriteLine(" done!");
    }

    private async Task PerformSingleFacetSearch(int count, int numberOfFacetFilterValues, List<TimeSpan> timings, string? searchTerm = null)
    {
        var facetDiscriminator = count % 5;
        var (facetGroup, facetValues) = facetDiscriminator == 0
            ? ("facetOne", _facetGroup1)
            : facetDiscriminator == 1
                ? ("facetTwo", _facetGroup2)
                : facetDiscriminator == 2
                    ? ("facetThree", _facetGroup3)
                    : facetDiscriminator == 3
                        ? ("facetFour", _facetGroup4)
                        : ("facetFive", _facetGroup5);

        var cultureDiscriminator = count % 3;
        var culture = cultureDiscriminator == 1 ? "en-US" : cultureDiscriminator == 2 ? "da-DK" : null;

        var start = DateTimeOffset.UtcNow;

        var result = await _searcher.SearchAsync(
            IndexAlias,
            culture: culture,
            query: searchTerm,
            filters: [new KeywordFilter(facetGroup, Randomize(facetValues, numberOfFacetFilterValues), false)],
            facets: [
                new KeywordFacet("facetOne"),
                new KeywordFacet("facetTwo"),
                new KeywordFacet("facetThree"),
                new KeywordFacet("facetFour"),
                new KeywordFacet("facetFive"),
            ]
        );

        timings.Add(DateTimeOffset.UtcNow - start);
    }

    private async Task PerformMultiFacetSearch(int count, int numberOfFacetFilterValues, List<TimeSpan> timings, string? searchTerm = null)
    {
        var facetDiscriminator = count % 5;
        (string FacetGroup, string[] FacetValues)[] filterGroups = facetDiscriminator == 0
            ? [("facetOne", _facetGroup1), ("facetTwo", _facetGroup2)]
            : facetDiscriminator == 1
                ? [("facetTwo", _facetGroup2), ("facetThree", _facetGroup3)]
                : facetDiscriminator == 2
                    ? [("facetThree", _facetGroup3), ("facetFour", _facetGroup4)]
                    : facetDiscriminator == 3
                        ? [("facetFour", _facetGroup4), ("facetFive", _facetGroup5)]
                        : [("facetFive", _facetGroup5), ("facetOne", _facetGroup1)];

        var cultureDiscriminator = count % 3;
        var culture = cultureDiscriminator == 1 ? "en-US" : cultureDiscriminator == 2 ? "da-DK" : "*";

        var start = DateTimeOffset.UtcNow;

        var result = await _searcher.SearchAsync(
            IndexAlias,
            culture: culture,
            query: searchTerm,
            filters: filterGroups
                .Select(group => new KeywordFilter(group.FacetGroup, Randomize(group.FacetValues, numberOfFacetFilterValues), false))
                .ToArray(),
            facets: [
                new KeywordFacet("facetOne"),
                new KeywordFacet("facetTwo"),
                new KeywordFacet("facetThree"),
                new KeywordFacet("facetFour"),
                new KeywordFacet("facetFive"),
            ]
        );

        timings.Add(DateTimeOffset.UtcNow - start);
    }

    private void PrintResults(List<TimeSpan> timings)
    {
        Console.WriteLine("Results:");
        Console.WriteLine($"- average query time: {timings.Average(t => t.Milliseconds):F2} ms");
        Console.WriteLine($"- maximum query time: {timings.Max(t => t.Milliseconds):F2} ms");
        Console.WriteLine($"- minimum query time: {timings.Min(t => t.Milliseconds):F2} ms");
        Console.WriteLine($"- P90 query time: {timings.OrderBy(t => t.Milliseconds).Take(90).Average(t => t.Milliseconds):F2} ms");
    }

    private T[] Randomize<T>(T[] items, int count)
        => items.OrderBy(_ => _randomizer.Next(items.Length * 100)).Take(count).ToArray();

    private static bool IsValidSearchTerm(string term) => term.Length > 3;

    private string RandomSearchTerm()
        => _searchTerms.OrderBy(_ => _randomizer.Next(_searchTerms.Count)).First();
}
