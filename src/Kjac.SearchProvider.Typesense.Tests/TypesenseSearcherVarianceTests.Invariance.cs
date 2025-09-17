using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;

namespace Kjac.SearchProvider.Typesense.Tests;

public partial class TypesenseSearcherVarianceTests
{
    [TestCase("en-US")]
    [TestCase("da-DK")]
    public async Task CanQuerySingleDocumentByInvariantField(string culture)
    {
        SearchResult result = await SearchAsync(
            query: "invariant12",
            culture: culture
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[12]));
            }
        );
    }

    [TestCase("en-US", "invariant", 100)]
    [TestCase("da-DK", "invariant", 100)]
    [TestCase("en-US", "invariant22", 1)]
    [TestCase("da-DK", "invariant33", 1)]
    // pending resolution of https://github.com/typesense/typesense/issues/2430
    // [TestCase("en-US", "invariant2", 11)] // 2 + 20-29
    // [TestCase("da-DK", "invariant3", 11)] // 3 + 30-39
    public async Task CanQueryMultipleDocumentsByInvariantField(string culture, string query, int expectedTotal)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(expectedTotal));
    }

    [TestCase("en-US")]
    [TestCase("da-DK")]
    public async Task CanQuerySingleInvariantDocumentByInvariantField(string culture)
    {
        SearchResult result = await SearchAsync(
            query: "commoninvariant78",
            culture: culture
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_invariantDocumentIds[78]));
            }
        );
    }

    [TestCase("en-US")]
    [TestCase("da-DK")]
    public async Task CanQueryAllInvariantAndVariantDocumentsByInvariantField(string culture)
    {
        SearchResult result = await SearchAsync(
            query: "commoninvariant",
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(200));
    }

    [TestCase("en-US")]
    [TestCase("da-DK")]
    public async Task CanFilterSingleDocumentByInvariantField(string culture)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldInvariance, ["invariant12"], false)],
            culture: culture
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_variantDocumentIds[12]));
            }
        );
    }

    [TestCase("en-US")]
    [TestCase("da-DK")]
    public async Task CanFilterAllDocumentsByInvariantField(string culture)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldInvariance, ["invariant"], false)],
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(100));
    }

    [TestCase("en-US")]
    [TestCase("da-DK")]
    public async Task CanFilterSingleInvariantDocumentByInvariantField(string culture)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldInvariance, ["commoninvariant78"], false)],
            culture: culture
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_invariantDocumentIds[78]));
            }
        );
    }

    [TestCase("en-US")]
    [TestCase("da-DK")]
    public async Task CanFilterAllInvariantAndVariantDocumentsByInvariantField(string culture)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldInvariance, ["commoninvariant"], false)],
            culture: culture
        );

        Assert.That(result.Total, Is.EqualTo(200));
    }

    [TestCase("english")]
    [TestCase("danish")]
    public async Task CannotQueryVariantFieldsWithInvariantSearch(string query)
    {
        SearchResult result = await SearchAsync(
            query: query,
            culture: null
        );

        Assert.That(result.Total, Is.EqualTo(0));
    }
}
