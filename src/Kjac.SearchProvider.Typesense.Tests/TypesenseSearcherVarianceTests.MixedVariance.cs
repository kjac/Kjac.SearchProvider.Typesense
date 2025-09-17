using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;

namespace Kjac.SearchProvider.Typesense.Tests;

public partial class TypesenseSearcherVarianceTests
{
    [TestCase("en-US", "english")]
    [TestCase("da-DK", "danish")]
    public async Task CanQuerySingleDocumentByMixedVariantField(string culture, string query)
    {
        SearchResult result = await SearchAsync(
            query: $"mixed{query}56",
            culture: culture
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[56]));
            }
        );
    }

    [TestCase("en-US", "mixedinvariant", 100)]
    [TestCase("en-US", "mixedenglish11", 1)]
    [TestCase("en-US", "mixeddanish", 0)]
    [TestCase("da-DK", "mixedinvariant", 100)]
    [TestCase("da-DK", "mixeddanish22", 1)]
    [TestCase("da-DK", "mixedenglish", 0)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    //[TestCase("en-US", "mixedenglish1", 11)] // 1 + 10-19
    //[TestCase("en-US", "mixedinvariant1", 11)] // 1 + 10-19
    //[TestCase("da-DK", "mixeddanish2", 11)] // 2 + 20-29
    //[TestCase("da-DK", "mixedinvariant2", 11)] // 2 + 20-29
    public async Task CanQueryMultipleDocumentsByMixedVariantField(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "mixedinvariant mixedenglish", 100)]
    [TestCase("en-US", "mixedinvariant11 mixedenglish", 1)]
    [TestCase("en-US", "mixedinvariant mixedenglish11", 1)]
    [TestCase("da-DK", "mixedinvariant mixeddanish", 100)]
    [TestCase("da-DK", "mixedinvariant22 mixeddanish", 1)]
    [TestCase("da-DK", "mixedinvariant mixeddanish22", 1)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    //[TestCase("en-US", "mixedinvariant1 mixedenglish1", 11)] // 1 + 10-19
    //[TestCase("da-DK", "mixedinvariant2 mixeddanish2", 11)] // 2 + 20-29
    public async Task CanQueryMixedVariantAndInvariantFieldsWithMixedVariantSearch(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US", "english")]
    [TestCase("en-US", "invariant")]
    [TestCase("da-DK", "danish")]
    [TestCase("da-DK", "invariant")]
    public async Task CanFilterSingleDocumentByMixedVariantField(string culture, string query)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldMixedVariance, [$"mixed{query}34"], false)],
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

    [TestCase("en-US", "mixedenglish", 100)]
    [TestCase("en-US", "mixedenglish11", 1)]
    [TestCase("en-US", "mixeddanish", 0)]
    [TestCase("en-US", "mixedinvariant", 100)]
    [TestCase("da-DK", "mixeddanish", 100)]
    [TestCase("da-DK", "mixeddanish22", 1)]
    [TestCase("da-DK", "mixedenglish", 0)]
    [TestCase("da-DK", "mixedinvariant", 100)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    //[TestCase("en-US", "mixedenglish1", 11)] // 1 + 10-19
    //[TestCase("en-US", "mixedinvariant1", 11)] // 1 + 10-19
    //[TestCase("da-DK", "mixeddanish2", 11)] // 2 + 20-29
    //[TestCase("da-DK", "mixedinvariant2", 11)] // 2 + 20-29
    public async Task CanFilterAllDocumentsByMixedVariantField(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldMixedVariance, [query], false)],
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }
}
