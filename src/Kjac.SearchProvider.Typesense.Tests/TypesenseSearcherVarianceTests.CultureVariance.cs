using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;

namespace Kjac.SearchProvider.Typesense.Tests;

public partial class TypesenseSearcherVarianceTests
{
    [TestCase("en-US", "english")]
    [TestCase("da-DK", "danish")]
    public async Task CanQuerySingleDocumentByVariantField(string culture, string query)
    {
        SearchResult result = await SearchAsync(
            query: $"{query}23",
            culture: culture
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[23]));
            }
        );
    }

    [TestCase("en-US", "english", 100)]
    [TestCase("en-US", "english11", 1)]
    [TestCase("en-US", "danish", 0)]
    [TestCase("da-DK", "danish", 100)]
    [TestCase("da-DK", "danish22", 1)]
    [TestCase("da-DK", "english", 0)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    //[TestCase("en-US", "english1", 11)] // 1 + 10-19
    //[TestCase("da-DK", "danish2", 11)] // 2 + 20-29
    public async Task CanQueryMultipleDocumentsByCultureVariantField(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "invariant", 100)]
    [TestCase("en-US", "invariant11", 1)]
    [TestCase("da-DK", "invariant", 100)]
    [TestCase("da-DK", "invariant22", 1)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    //[TestCase("en-US", "invariant1", 11)] // 1 + 10-19
    //[TestCase("da-DK", "invariant2", 11)] // 2 + 20-29
    public async Task CanQueryInvariantFieldsWithVariantSearch(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "invariant english", 100)]
    [TestCase("en-US", "invariant11 english", 1)]
    [TestCase("en-US", "invariant english11", 1)]
    [TestCase("da-DK", "invariant danish", 100)]
    [TestCase("da-DK", "invariant22 danish", 1)]
    [TestCase("da-DK", "invariant danish22", 1)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    //[TestCase("en-US", "invariant1 english1", 11)] // 1 + 10-19
    //[TestCase("da-DK", "invariant2 danish2", 11)] // 2 + 20-29
    public async Task CanQueryMixedVariantAndInvariantFieldsWithVariantSearch(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "english")]
    [TestCase("da-DK", "danish")]
    public async Task CanFilterSingleDocumentByCultureVariantField(string culture, string query)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldCultureVariance, [$"{query}34"], false)],
            culture: culture
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[34]));
            }
        );
    }

    [TestCase("en-US", "english", 100)]
    [TestCase("en-US", "english11", 1)]
    [TestCase("en-US", "danish", 0)]
    [TestCase("da-DK", "danish", 100)]
    [TestCase("da-DK", "danish22", 1)]
    [TestCase("da-DK", "english", 0)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    //[TestCase("en-US", "english1", 11)] // 1 + 10-19
    //[TestCase("da-DK", "danish2", 11)] // 2 + 20-29
    public async Task CanFilterAllDocumentsByCultureVariantField(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldCultureVariance, [query], false)],
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }
}
