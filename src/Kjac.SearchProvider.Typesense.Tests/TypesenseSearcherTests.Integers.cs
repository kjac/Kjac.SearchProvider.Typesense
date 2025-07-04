using Umbraco.Cms.Core;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;
using Umbraco.Extensions;

namespace Kjac.SearchProvider.Typesense.Tests;

// tests specifically related to the IndexValue.Integers collection
public partial class TypesenseSearcherTests
{
    [Test]
    public async Task CanFilterSingleDocumentByIntegerExact()
    {
        SearchResult result = await SearchAsync(
            filters: [new IntegerExactFilter(FieldMultipleValues, [1], false)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_documentIds[1]));
            }
        );
    }

    [Test]
    public async Task CanFilterSingleDocumentByNegativeIntegerExact()
    {
        SearchResult result = await SearchAsync(
            filters: [new IntegerExactFilter(FieldMultipleValues, [-2], false)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_documentIds[2]));
            }
        );
    }

    [Test]
    public async Task CanFilterSingleDocumentByIntegerRange()
    {
        SearchResult result = await SearchAsync(
            filters: [new IntegerRangeFilter(FieldMultipleValues, [new FilterRange<int?>(1, 2)], false)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_documentIds[1]));
            }
        );
    }

    [Test]
    public async Task CanFilterSingleDocumentByNegativeIntegerRange()
    {
        SearchResult result = await SearchAsync(
            filters: [new IntegerRangeFilter(FieldMultipleValues, [new FilterRange<int?>(-2, -1)], false)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(1));
                Assert.That(result.Documents.First().Id, Is.EqualTo(_documentIds[2]));
            }
        );
    }

    [Test]
    public async Task CanFilterMultipleDocumentsByIntegerExact()
    {
        SearchResult result = await SearchAsync(
            filters: [new IntegerExactFilter(FieldMultipleValues, [10, 50, 100], false)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(5));

                var documents = result.Documents.ToList();
                // expecting 1 (10), 5 (50), 10 (10 + 100), 50 (50) and 100 (100)
                Assert.That(
                    documents.Select(d => d.Id),
                    Is.EquivalentTo(
                        new[]
                        {
                            _documentIds[1], _documentIds[5], _documentIds[10], _documentIds[50], _documentIds[100]
                        }
                    )
                );
            }
        );
    }

    [Test]
    public async Task CanFilterMultipleDocumentsByIntegerRange()
    {
        SearchResult result = await SearchAsync(
            filters:
            [
                new IntegerRangeFilter(
                    FieldMultipleValues,
                    [new FilterRange<int?>(1, 5), new FilterRange<int?>(20, 25), new FilterRange<int?>(100, 101)],
                    false
                )
            ]
        );

        Assert.Multiple(
            () =>
            {
                // expecting
                // - first range: 1, 2, 3, 4
                // - second range: 2 (20), 20, 21, 22, 23, 24
                // - third range: 10 (100), 100
                Assert.That(result.Total, Is.EqualTo(11));

                var documents = result.Documents.ToList();
                Assert.That(
                    documents.Select(d => d.Id),
                    Is.EquivalentTo(
                        new[]
                        {
                            _documentIds[1],
                            _documentIds[2],
                            _documentIds[3],
                            _documentIds[4],
                            _documentIds[10],
                            _documentIds[20],
                            _documentIds[21],
                            _documentIds[22],
                            _documentIds[23],
                            _documentIds[24],
                            _documentIds[100],
                        }
                    )
                );
            }
        );
    }

    [Test]
    public async Task CanFilterDocumentsByIntegerExactNegated()
    {
        SearchResult result = await SearchAsync(
            filters: [new IntegerExactFilter(FieldMultipleValues, [1], true)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(99));
                Assert.That(result.Documents.Select(d => d.Id), Is.EquivalentTo(_documentIds.Values.Skip(1)));
            }
        );
    }

    [Test]
    public async Task CanFilterDocumentsByIntegerRangeNegated()
    {
        SearchResult result = await SearchAsync(
            filters: [new IntegerRangeFilter(FieldMultipleValues, [new FilterRange<int?>(1, 2)], true)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(99));
                Assert.That(result.Documents.Select(d => d.Id), Is.EquivalentTo(_documentIds.Values.Skip(1)));
            }
        );
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CanFacetDocumentsByIntegerExact(bool filtered)
    {
        SearchResult result = await SearchAsync(
            facets: [new IntegerExactFacet(FieldMultipleValues)],
            filters: filtered ? [new IntegerExactFilter(FieldMultipleValues, [1, 2, 3], false)] : []
        );

        // expecting the same facets whether filtering is enabled or not, because
        // both faceting and filtering is applied to the same field
        var expectedFacetValues = Enumerable
            .Range(1, 100)
            .SelectMany(i => new[] { i, i * 10, i * -1, i * -10 })
            .GroupBy(i => i)
            .Select(group => new { Key = group.Key, Count = group.Count() })
            .ToArray();

        // expecting
        // - when filtered: 1, 2 and 3
        // - when not filtered: all of them
        Assert.That(result.Total, Is.EqualTo(filtered ? 3 : 100));

        FacetResult[] facets = result.Facets.ToArray();
        Assert.That(facets, Has.Length.EqualTo(1));

        FacetResult facet = facets.First();
        Assert.That(facet.FieldName, Is.EqualTo(FieldMultipleValues));

        IntegerExactFacetValue[] facetValues = facet.Values.OfType<IntegerExactFacetValue>().ToArray();
        Assert.That(facetValues, Has.Length.EqualTo(expectedFacetValues.Length));
        foreach (var expectedFacetValue in expectedFacetValues)
        {
            IntegerExactFacetValue? facetValue = facetValues.FirstOrDefault(f => f.Key == expectedFacetValue.Key);
            Assert.That(facetValue, Is.Not.Null);
            Assert.That(facetValue.Count, Is.EqualTo(expectedFacetValue.Count));
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CanFacetDocumentsByIntegerRange(bool filtered)
    {
        SearchResult result = await SearchAsync(
            facets:
            [
                new IntegerRangeFacet(
                    FieldMultipleValues,
                    [
                        new IntegerRangeFacetRange("One", 1, 25),
                        new IntegerRangeFacetRange("Two", 25, 50),
                        new IntegerRangeFacetRange("Three", 50, 75),
                        new IntegerRangeFacetRange("Four", 75, 100)
                    ]
                )
            ],
            filters: filtered ? [new IntegerExactFilter(FieldMultipleValues, [1, 2, 3], false)] : []
        );

        // expecting the same facets whether filtering is enabled or not, because
        // both faceting and filtering is applied to the same field
        var expectedFacetValues = new[]
        {
            new { Key = "One", Count = 24 }, // 1-24
            new { Key = "Two", Count = 25 }, // 25-49
            new { Key = "Three", Count = 25 }, // 50-74
            new { Key = "Four", Count = 25 }, // 75-99
        };

        // expecting
        // - when filtered: 1, 2 and 3
        // - when not filtered: all of them
        Assert.That(result.Total, Is.EqualTo(filtered ? 3 : 100));

        FacetResult[] facets = result.Facets.ToArray();
        Assert.That(facets, Has.Length.EqualTo(1));

        FacetResult facet = facets.First();
        Assert.That(facet.FieldName, Is.EqualTo(FieldMultipleValues));

        IntegerRangeFacetValue[] facetValues = facet.Values.OfType<IntegerRangeFacetValue>().ToArray();
        Assert.That(facetValues, Has.Length.EqualTo(expectedFacetValues.Length));
        foreach (var expectedFacetValue in expectedFacetValues)
        {
            IntegerRangeFacetValue? facetValue = facetValues.FirstOrDefault(f => f.Key == expectedFacetValue.Key);
            Assert.That(facetValue, Is.Not.Null);
            Assert.That(facetValue.Count, Is.EqualTo(expectedFacetValue.Count));
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CanSortDocumentsByInteger(bool ascending)
    {
        SearchResult result = await SearchAsync(
            sorters: [new IntegerSorter(FieldSingleValue, ascending ? Direction.Ascending : Direction.Descending)]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Total, Is.EqualTo(100));
                Assert.That(result.Documents.First().Id, Is.EqualTo(ascending ? _documentIds[1] : _documentIds[100]));
            }
        );
    }
}
