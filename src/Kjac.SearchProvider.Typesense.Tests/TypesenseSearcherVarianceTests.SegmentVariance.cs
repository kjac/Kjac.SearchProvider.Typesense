using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;

namespace Kjac.SearchProvider.Typesense.Tests;

public partial class TypesenseSearcherVarianceTests
{
    [TestCase("en-US", "seg1", "seg1english")]
    [TestCase("en-US", "seg2", "seg2english")]
    [TestCase("da-DK", "seg1", "seg1danish")]
    public async Task CanQuerySingleDocumentBySegmentVariantField(string culture, string segment, string query)
    {
        SearchResult result = await SearchAsync(
            query: $"{query}23",
            culture: culture,
            segment: segment
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[23]));
            }
        );
    }

    [TestCase("en-US", "seg1", "seg1english", 100)]
    [TestCase("en-US", "seg1", "seg1english11", 1)]
    [TestCase("en-US", "seg1", "seg1danish", 0)]
    [TestCase("da-DK", "seg1", "seg1danish", 100)]
    [TestCase("da-DK", "seg1", "seg1danish22", 1)]
    [TestCase("da-DK", "seg1", "seg1english", 0)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    // [TestCase("en-US", "seg1", "seg1english1", 12)] // 1 + 10-19 + 100
    // [TestCase("da-DK", "seg1", "seg1danish2", 11)] // 2 + 20-29
    public async Task CanQueryMultipleDocumentsBySegmentVariantField(string culture, string segment, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture,
            segment: segment
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "invariant", 100)]
    [TestCase("en-US", "invariant11", 1)]
    [TestCase("da-DK", "invariant", 100)]
    [TestCase("da-DK", "invariant22", 1)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    // [TestCase("en-US", "invariant1", 12)] // 1 + 10-19 + 100
    // [TestCase("da-DK", "invariant2", 11)] // 2 + 20-29
    public async Task CanQueryInvariantFieldsWithSegmentVariantSearch(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture,
            segment: "seg1"
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "english", 100)]
    [TestCase("en-US", "english11", 1)]
    [TestCase("da-DK", "danish", 100)]
    [TestCase("da-DK", "danish22", 1)]
    public async Task CanQueryCultureVariantFieldsWithSegmentVariantSearch(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture,
            segment: "seg1"
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "seg1", "invariant seg1english", 100)]
    [TestCase("en-US", "seg1", "invariant11 seg1english", 1)]
    [TestCase("en-US", "seg1", "invariant seg1english11", 1)]
    [TestCase("en-US", "seg1", "invariant1 seg1english1", 1)]
    [TestCase("da-DK", "seg1", "invariant seg1danish", 100)]
    [TestCase("da-DK", "seg1", "invariant22 seg1danish", 1)]
    [TestCase("da-DK", "seg1", "invariant seg1danish22", 1)]
    [TestCase("da-DK", "seg1", "invariant2 seg1danish2", 1)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    // [TestCase("en-US", "seg1", "invariant10 seg1english12", 0)]
    // [TestCase("da-DK", "seg1", "invariant20 seg1danish22", 0)]
    public async Task CanQueryMixedSegmentVariantAndInvariantFieldsWithVariantSearch(string culture, string segment, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture,
            segment: segment
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "seg1", "seg1english")]
    [TestCase("en-US", "seg2", "seg2english")]
    [TestCase("da-DK", "seg1", "seg1danish")]
    public async Task CanFilterSingleDocumentBySegmentVariantTextField(string culture, string segment, string query)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldSegmentVariance, [$"{query}34"], false)],
            culture: culture,
            segment: segment
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[34]));
            }
        );
    }

    [TestCase("en-US", "seg1", "seg1english", 100)]
    [TestCase("en-US", "seg1", "seg1english11", 1)]
    [TestCase("en-US", "seg1", "seg1danish", 0)]
    [TestCase("en-US", "seg2", "seg2english", 100)]
    [TestCase("en-US", "seg2", "seg2english33", 1)]
    [TestCase("en-US", "seg2", "seg2danish", 0)]
    [TestCase("da-DK", "seg1", "seg1danish", 100)]
    [TestCase("da-DK", "seg1", "seg1danish22", 1)]
    [TestCase("da-DK", "seg1", "seg1english", 0)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    // [TestCase("da-DK", "seg1", "seg1danish2", 11)] // 2 + 20-29
    // [TestCase("en-US", "seg1", "seg1english1", 12)] // 1 + 10-19 + 100
    // [TestCase("en-US", "seg2", "seg2english3", 11)] // 3 + 30-39
    public async Task CanFilterAllDocumentsBySegmentVariantTextField(string culture, string segment, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldSegmentVariance, [query], false)],
            culture: culture,
            segment: segment
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "seg1", "defaultkeywordenglish")]
    [TestCase("en-US", "seg2", "defaultkeywordenglish")]
    [TestCase("da-DK", "seg1", "defaultkeyworddanish")]
    [TestCase("en-US", "seg1", "seg1keywordenglish")]
    [TestCase("en-US", "seg2", "seg2keywordenglish")]
    [TestCase("da-DK", "seg1", "seg1keyworddanish")]
    public async Task CanFilterSingleDocumentBySegmentVariantKeywordField(string culture, string segment, string query)
    {
        SearchResult result = await SearchAsync(
            filters: [new KeywordFilter(FieldSegmentVariance, [$"{query}34"], false)],
            culture: culture,
            segment: segment
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[34]));
            }
        );
    }

    [TestCase("en-US", "seg1", "defaultkeywordenglish", 100)]
    [TestCase("en-US", "seg1", "defaultkeywordenglish11", 1)]
    [TestCase("da-DK", "seg1", "defaultkeyworddanish", 100)]
    [TestCase("da-DK", "seg1", "defaultkeyworddanish22", 1)]
    [TestCase("en-US", "seg1", "seg1keywordenglish", 100)]
    [TestCase("en-US", "seg1", "seg1keywordenglish11", 1)]
    [TestCase("en-US", "seg1", "seg1keyworddanish", 0)]
    [TestCase("en-US", "seg1", "seg1keywordenglish1", 1)]
    [TestCase("en-US", "seg2", "seg2keywordenglish", 100)]
    [TestCase("en-US", "seg2", "seg2keywordenglish33", 1)]
    [TestCase("en-US", "seg2", "seg2keyworddanish", 0)]
    [TestCase("en-US", "seg2", "seg2keywordenglish3", 1)]
    [TestCase("da-DK", "seg1", "seg1keyworddanish", 100)]
    [TestCase("da-DK", "seg1", "seg1keyworddanish22", 1)]
    [TestCase("da-DK", "seg1", "seg1keywordenglish", 0)]
    [TestCase("da-DK", "seg1", "seg1keyworddanish2", 1)]
    public async Task CanFilterAllDocumentsBySegmentVariantKeywordField(string culture, string segment, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            filters: [new KeywordFilter(FieldSegmentVariance, [query], false)],
            culture: culture,
            segment: segment
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "seg1", 34)] // default segment variant value (English)
    [TestCase("en-US", "seg1", 134)] // seg1 segment variant value (English)
    [TestCase("en-US", "seg2", 434)] // seg2 segment variant value (English)
    [TestCase("da-DK", "seg1", 234)] // default segment variant value (Danish)
    [TestCase("da-DK", "seg1", 334)] // seg1 segment variant value (Danish)
    public async Task CanFilterSingleDocumentBySegmentVariantIntegerField(string culture, string segment, int value)
    {
        SearchResult result = await SearchAsync(
            filters: [new IntegerExactFilter(FieldSegmentVariance, [value], false)],
            culture: culture,
            segment: segment
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[34]));
            }
        );
    }

    [TestCase("en-US", "seg1", 34)] // default segment variant value (English)
    [TestCase("en-US", "seg1", 134)] // seg1 segment variant value (English)
    [TestCase("en-US", "seg2", 434)] // seg2 segment variant value (English)
    [TestCase("da-DK", "seg1", 234)] // default segment variant value (Danish)
    [TestCase("da-DK", "seg1", 334)] // seg1 segment variant value (Danish)
    public async Task CanFilterSingleDocumentBySegmentVariantIntegerFieldRange(string culture, string segment, int value)
    {
        SearchResult result = await SearchAsync(
            filters: [new IntegerRangeFilter(FieldSegmentVariance, [new IntegerRangeFilterRange(value, value + 1)], false)],
            culture: culture,
            segment: segment
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[34]));
            }
        );
    }

    [TestCase("en-US", "seg1", 34)] // default segment variant value (English)
    [TestCase("en-US", "seg1", 134)] // seg1 segment variant value (English)
    [TestCase("en-US", "seg2", 434)] // seg2 segment variant value (English)
    [TestCase("da-DK", "seg1", 234)] // default segment variant value (Danish)
    [TestCase("da-DK", "seg1", 334)] // seg1 segment variant value (Danish)
    public async Task CanFilterSingleDocumentBySegmentVariantDecimalField(string culture, string segment, decimal value)
    {
        SearchResult result = await SearchAsync(
            filters: [new DecimalExactFilter(FieldSegmentVariance, [value], false)],
            culture: culture,
            segment: segment
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[34]));
            }
        );
    }

    [TestCase("en-US", "seg1", 34)] // default segment variant value (English)
    [TestCase("en-US", "seg1", 134)] // seg1 segment variant value (English)
    [TestCase("en-US", "seg2", 434)] // seg2 segment variant value (English)
    [TestCase("da-DK", "seg1", 234)] // default segment variant value (Danish)
    [TestCase("da-DK", "seg1", 334)] // seg1 segment variant value (Danish)
    public async Task CanFilterSingleDocumentBySegmentVariantDecimalFieldRange(string culture, string segment, decimal value)
    {
        SearchResult result = await SearchAsync(
            filters: [new DecimalRangeFilter(FieldSegmentVariance, [new DecimalRangeFilterRange(value, value + 1)], false)],
            culture: culture,
            segment: segment
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[34]));
            }
        );
    }

    [TestCase("en-US", "seg1", 34)] // default segment variant value (English)
    [TestCase("en-US", "seg1", 134)] // seg1 segment variant value (English)
    [TestCase("en-US", "seg2", 434)] // seg2 segment variant value (English)
    [TestCase("da-DK", "seg1", 234)] // default segment variant value (Danish)
    [TestCase("da-DK", "seg1", 334)] // seg1 segment variant value (Danish)
    public async Task CanFilterSingleDocumentBySegmentVariantDateTimeOffsetField(string culture, string segment, int daysOffset)
    {
        SearchResult result = await SearchAsync(
            filters: [new DateTimeOffsetExactFilter(FieldSegmentVariance, [StartDate().AddDays(daysOffset)], false)],
            culture: culture,
            segment: segment
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[34]));
            }
        );
    }

    [TestCase("en-US", "seg1", 34)] // default segment variant value (English)
    [TestCase("en-US", "seg1", 134)] // seg1 segment variant value (English)
    [TestCase("en-US", "seg2", 434)] // seg2 segment variant value (English)
    [TestCase("da-DK", "seg1", 234)] // default segment variant value (Danish)
    [TestCase("da-DK", "seg1", 334)] // seg1 segment variant value (Danish)
    public async Task CanFilterSingleDocumentBySegmentVariantDateTimeOffsetFieldRange(string culture, string segment, int daysOffset)
    {
        SearchResult result = await SearchAsync(
            filters: [new DateTimeOffsetRangeFilter(FieldSegmentVariance, [new DateTimeOffsetRangeFilterRange(StartDate().AddDays(daysOffset), StartDate().AddDays(daysOffset + 1))], false)],
            culture: culture,
            segment: segment
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[34]));
            }
        );
    }

    [TestCase("en-US", "seg1", "seg1english11", 1)]
    [TestCase("en-US", "seg2", "seg2english11", 1)]
    [TestCase("da-DK", "seg1", "seg1danish22", 1)]
    [TestCase("en-US", null, "seg1english11", 0)]
    [TestCase("en-US", null, "seg2english11", 0)]
    [TestCase("da-DK", null, "seg1danish22", 0)]
    public async Task CannotQuerySegmentValueWithoutSegment(string culture, string? segment, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture,
            segment: segment
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    // [TestCase("da-DK", "seg2", "seg1danish22")]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    // [TestCase("en-US", "seg1", "seg2english11")]
    // [TestCase("en-US", "seg2", "seg1english11")]
    public async Task CannotQueryBetweenSegments(string culture, string? segment, string query)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture,
            segment: segment
        );

        Assert.That(result.Total, Is.Zero);
    }
}
