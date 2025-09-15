using Umbraco.Cms.Core;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;

namespace Kjac.SearchProvider.Typesense.Tests;

// tests specifically related to the IndexValue.Texts collection
public partial class TypesenseSearcherTests
{
    [Test]
    public async Task CanFilterSingleDocumentBySpecificText()
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldMultipleValues, ["single12"], false)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_documentIds[12]));
            }
        );
    }

    [Test]
    public async Task CanFilterMultipleDocumentsBySpecificText()
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldMultipleValues, ["single11", "single22", "single33"], false)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(3));

                var documents = result.Documents.ToList();
                Assert.That(
                    documents.Select(d => d.Id),
                    Is.EquivalentTo(new[] { _documentIds[11], _documentIds[22], _documentIds[33] })
                );
            }
        );
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CanFilterMultipleDocumentsByCommonText(bool even)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldMultipleValues, [even ? "even" : "odd"], false)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(50));

                var documents = result.Documents.ToList();
                var expectedIds = OddOrEvenIds(even);
                Assert.That(
                    documents.Select(d => d.Id),
                    Is.EquivalentTo(expectedIds.Select(id => _documentIds[id]))
                );
            }
        );
    }

    [Test]
    public async Task CanFilterDocumentsBySpecificTextNegated()
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldMultipleValues, ["single12"], true)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(99));
                Assert.That(
                    result.Documents.Select(d => d.Id),
                    Is.EquivalentTo(_documentIds.Values.Except([_documentIds[12]]))
                );
            }
        );
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CanFilterDocumentsByCommonTextNegated(bool even)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldMultipleValues, [even ? "even" : "odd"], true)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(50));

                var documents = result.Documents.ToList();
                var expectedIds = OddOrEvenIds(even is false);
                Assert.That(
                    documents.Select(d => d.Id),
                    Is.EquivalentTo(expectedIds.Select(id => _documentIds[id]))
                );
            }
        );
    }

    [Test]
    [Ignore("Pending resolution of https://github.com/typesense/typesense/issues/2430")]
    public async Task CanFilterAllDocumentsByWildcardText()
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldMultipleValues, ["single"], false)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(100));
                Assert.That(result.Documents.Select(d => d.Id), Is.EqualTo(_documentIds.Values).AsCollection);
            }
        );
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CanFilterAllDocumentsByWildcardTextSortedByTextualRelevance(bool ascending)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldTextRelevance, ["spec"], false)],
            sorters: [new ScoreSorter(ascending ? Direction.Ascending : Direction.Descending)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(4));

                Guid[] expectedDocumentIdsByOrderOfRelevance =
                [
                    _documentIds[30], // TextsR1
                    _documentIds[20], // TextsR2
                    _documentIds[40], // TextsR3
                    _documentIds[10] // Texts
                ];
                if (ascending)
                {
                    expectedDocumentIdsByOrderOfRelevance = expectedDocumentIdsByOrderOfRelevance.Reverse().ToArray();
                }

                Assert.That(
                    result.Documents.Select(d => d.Id),
                    Is.EqualTo(expectedDocumentIdsByOrderOfRelevance).AsCollection
                );
            }
        );
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CanSortDocumentsByText(bool ascending)
    {
        SearchResult result = await SearchAsync(
            sorters: [new TextSorter(FieldSingleValue, ascending ? Direction.Ascending : Direction.Descending)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(100));
                Assert.That(result.Documents.First().Id, Is.EqualTo(ascending ? _documentIds[1] : _documentIds[99]));
            }
        );
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CanSortDocumentsByTextualRelevance(bool ascending)
    {
        SearchResult result = await SearchAsync(
            filters: [new TextFilter(FieldTextRelevance, ["special"], false)],
            sorters: [new TextSorter(FieldMultiSorting, ascending ? Direction.Ascending : Direction.Descending)]
        );

        Assert.That(result.Total, Is.EqualTo(4));

        Assert.Multiple(
            () =>
            {
                Guid[] expectedDocumentIdsByOrderOfRelevance =
                [
                    _documentIds[10], // TextsR1 ("sortable_a")
                    _documentIds[20], // TextsR2 ("sortable_b")
                    _documentIds[30], // TextsR3 ("sortable_c")
                    _documentIds[40] // Texts ("sortable_d")
                ];
                if (ascending is false)
                {
                    expectedDocumentIdsByOrderOfRelevance = expectedDocumentIdsByOrderOfRelevance.Reverse().ToArray();
                }

                Assert.That(
                    result.Documents.Select(d => d.Id),
                    Is.EqualTo(expectedDocumentIdsByOrderOfRelevance).AsCollection
                );
            }
        );
    }
}
