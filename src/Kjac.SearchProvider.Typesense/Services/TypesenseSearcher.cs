using System.Globalization;
using System.Text.Json.Serialization;
using Kjac.SearchProvider.Typesense.Configuration;
using Kjac.SearchProvider.Typesense.Constants;
using Kjac.SearchProvider.Typesense.Extensions;
using Kjac.SearchProvider.Typesense.Models.Searching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Typesense;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;
using Umbraco.Extensions;

namespace Kjac.SearchProvider.Typesense.Services;

internal sealed class TypesenseSearcher : TypesenseServiceBase, ITypesenseSearcher
{
    private readonly ITypesenseClient _typesenseClient;
    private readonly SearcherOptions _searcherOptions;
    private readonly ILogger<TypesenseSearcher> _logger;

    public TypesenseSearcher(
        ITypesenseClient typesenseClient,
        IOptions<SearcherOptions> options,
        ILogger<TypesenseSearcher> logger)
    {
        _typesenseClient = typesenseClient;
        _searcherOptions = options.Value;
        _logger = logger;
    }

    // TODO: split this method into multiple!
    public async Task<SearchResult> SearchAsync(
        string indexAlias,
        string? query = null,
        IEnumerable<Filter>? filters = null,
        IEnumerable<Facet>? facets = null,
        IEnumerable<Sorter>? sorters = null,
        string? culture = null,
        string? segment = null,
        AccessContext? accessContext = null,
        int skip = 0,
        int take= 10)
    {
        if (query is null && filters is null && facets is null && sorters is null)
        {
            return new SearchResult(0, [], []);
        }

        PaginationHelper.ConvertSkipTakeToPaging(skip, take, out var pageNumber, out var pageSize);
        // Typesense paging is 1 based
        pageNumber++;

        Filter[] filtersAsArray = filters as Filter[] ?? filters?.ToArray() ?? [];

        if (culture is not null)
        {
            filtersAsArray = new Filter[] { new SystemFieldFilter(IndexConstants.FieldNames.Culture, culture, false) }
                .Union(filtersAsArray)
                .ToArray();
        }

        // explicitly ignore duplicate facets
        Facet[] facetsAsArray = facets as Facet[] ?? facets?
            .GroupBy(FieldName)
            .Select(group => group.First())
            .ToArray()
            ?? [];
        var facetFieldNames = facetsAsArray.Select(facet => facet.FieldName).ToArray();
        Filter[] facetFilters = filtersAsArray.Where(f => facetFieldNames.InvariantContains(f.FieldName)).ToArray();

        string FilterValue(string fieldName, IEnumerable<string> values, Filter filter)
            => $"{fieldName}:{(filter.Negate ? "!=" : filter is KeywordFilter ? "=" : null)}[{string.Join(",", values)}]";

        string TextFilterValue(TextFilter textFilter)
        {
            var values = textFilter.Values.Select(value => $"`{value}*`").ToArray();
            var fieldNameTexts = FieldName(textFilter.FieldName, IndexConstants.FieldTypePostfix.Texts);
            var fieldNameTextsR1 = FieldName(textFilter.FieldName, IndexConstants.FieldTypePostfix.TextsR1);
            var fieldNameTextsR2 = FieldName(textFilter.FieldName, IndexConstants.FieldTypePostfix.TextsR2);
            var fieldNameTextsR3 = FieldName(textFilter.FieldName, IndexConstants.FieldTypePostfix.TextsR3);

            var filterOperator = textFilter.Negate ? "&&" : "||";
            return $"({FilterValue(fieldNameTextsR1, values, textFilter)} {filterOperator} {FilterValue(fieldNameTextsR2, values, textFilter)} {filterOperator} {FilterValue(fieldNameTextsR3, values, textFilter)} {filterOperator} {FilterValue(fieldNameTexts, values, textFilter)})";
        }

        string FilterByValue(IEnumerable<Filter> filterByFilters)
            => string.Join(
                " && ",
                filterByFilters.Select(
                    filter => filter is TextFilter textFilter
                        ? TextFilterValue(textFilter)
                        : FilterValue(
                            FieldName(filter),
                            filter switch
                            {
                                KeywordFilter keywordFilter => keywordFilter.Values.Select(value => $"`{value}`"),
                                IntegerExactFilter integerExactFilter => integerExactFilter.Values.Select(
                                    value => value.ToString()
                                ),
                                IntegerRangeFilter integerRangeFilter => integerRangeFilter.Ranges.Select(
                                    range
                                        => $"{range.Min ?? int.MinValue}..{(range.Max ?? int.MaxValue) - 1}"
                                ),
                                DecimalExactFilter decimalExactFilter => decimalExactFilter.Values.Select(DecimalValue),
                                DecimalRangeFilter decimalRangeFilter => decimalRangeFilter.Ranges.Select(
                                    range
                                        => $"{DecimalValue(range.Min ?? decimal.MinValue)}..{DecimalValue((range.Max ?? decimal.MaxValue) - 0.01m)}"
                                ),
                                // Typesense expects unix timestamps for dates
                                DateTimeOffsetExactFilter dateTimeOffsetExactFilter => dateTimeOffsetExactFilter.Values
                                    .Select(
                                        value => value.ToUnixTimeSeconds().ToString()
                                    ),
                                DateTimeOffsetRangeFilter dateTimeOffsetRangeFilter => dateTimeOffsetRangeFilter.Ranges
                                    .Select(
                                        range
                                            => $"{(range.Min ?? DateTimeOffset.UnixEpoch).ToUnixTimeSeconds()}..{(range.Max ?? DateTimeOffset.MaxValue).ToUnixTimeSeconds() - 1}"
                                    ),
                                SystemFieldFilter systemFieldFilter => [$"`{systemFieldFilter.Value}`"],
                                _ => throw new ArgumentOutOfRangeException(
                                    nameof(filter),
                                    $"Encountered an unsupported filter type: {filter.GetType().Name}"
                                )
                            },
                            filter
                        )
                )
            );

        string SortByValue(Sorter[] effectiveSorters, Filter[] effectiveFilters, string? effectiveQuery)
        {
            return string.Join(
                ",",
                effectiveSorters
                    .Select(
                        sorter =>
                        {
                            var direction = sorter.Direction is Direction.Ascending ? "asc" : "desc";

                            if (sorter is ScoreSorter)
                            {
                                string ValidEvalValue(string? value)
                                    => value ?? string.Empty;

                                var queryParts = new List<string> { ValidEvalValue(effectiveQuery?.Trim("*")) };

                                // NOTE: filter order matters here
                                foreach (Filter filter in effectiveFilters)
                                {
                                    if (filter is TextFilter textFilter)
                                    {
                                        queryParts.AddRange(textFilter.Values.Select(ValidEvalValue));
                                    }

                                    if (filter is KeywordFilter keywordFilter)
                                    {
                                        queryParts.AddRange(keywordFilter.Values.Select(ValidEvalValue));
                                    }
                                }

                                effectiveQuery = string.Join(" ", queryParts).Trim();

                                if (effectiveQuery.IsNullOrWhiteSpace() is false)
                                {
                                    return $"_eval([({IndexConstants.FieldNames.AllTextsR1}:`{effectiveQuery}`*):{_searcherOptions.BoostFactorTextR1}, ({IndexConstants.FieldNames.AllTextsR2}:`{effectiveQuery}`*):{_searcherOptions.BoostFactorTextR2}, ({IndexConstants.FieldNames.AllTextsR3}:`{effectiveQuery}`*):{_searcherOptions.BoostFactorTextR3}]):{direction}";
                                }
                            }

                            return $"{FieldName(sorter)}:{direction}";
                        }
                    )
            );
        }

        try
        {
            // must have a query - wildcard if nothing else
            query ??= "*";

            Sorter[] sortersAsArray = sorters as Sorter[] ?? sorters?.ToArray() ?? [];

            MultiSearchParameters CreateMultiSearchParameters(
                Filter[] multiSearchFilters,
                string? facetBy,
                bool includeDocuments)
                => new(
                    indexAlias.ValidIndexAlias(),
                    query,
                    $"{IndexConstants.FieldNames.AllTextsR1},{IndexConstants.FieldNames.AllTextsR2},{IndexConstants.FieldNames.AllTextsR3},{IndexConstants.FieldNames.AllTexts}")
                {
                    QueryByWeights = $"{_searcherOptions.BoostFactorTextR1},{_searcherOptions.BoostFactorTextR2},{_searcherOptions.BoostFactorTextR3},1",
                    FilterBy = multiSearchFilters.Length > 0 ? FilterByValue(multiSearchFilters) : null,
                    FacetBy = facetBy,
                    MaxFacetValues = _searcherOptions.MaxFacetValues,
                    Page = includeDocuments ? (int)pageNumber : 0,
                    PerPage = includeDocuments ? pageSize : 0,
                    SortBy = includeDocuments
                        ? SortByValue(sortersAsArray, multiSearchFilters, query)
                        : null,
                    HighlightFields = "none",
                    ValidateFieldNames = false
                };

            string? FacetBy(Facet[] effectiveFacets)
            {
                if (effectiveFacets.Length == 0)
                {
                    return null;
                }

                return string.Join(
                    ",",
                    effectiveFacets.Select(
                        facet =>
                            facet switch
                            {
                                IntegerExactFacet or DecimalExactFacet or DateTimeOffsetExactFacet or KeywordFacet
                                    => FieldName(facet),
                                IntegerRangeFacet { Ranges.Length: > 0 } integerRangeFacet
                                    => $"{FieldName(facet)}({string.Join(",", integerRangeFacet.Ranges.Select(range => $"{range.Key}:[{range.Min},{range.Max}]"))})",
                                DecimalRangeFacet { Ranges.Length: > 0 } decimalRangeFacet
                                    => $"{FieldName(facet)}({string.Join(",", decimalRangeFacet.Ranges.Select(range => $"{range.Key}:[{DecimalValue(range.Min)},{DecimalValue(range.Max)}]"))})",
                                DateTimeOffsetRangeFacet { Ranges.Length: > 0 } dateTimeOffsetRangeFacet
                                    =>
                                    $"{FieldName(facet)}({string.Join(",", dateTimeOffsetRangeFacet.Ranges.Select(range => $"{range.Key}:[{range.Min?.ToUnixTimeSeconds()},{range.Max?.ToUnixTimeSeconds()}]"))})",
                                _ => throw new ArgumentOutOfRangeException(
                                    nameof(facet),
                                    $"Encountered an unsupported facet type: {facet.GetType().Name}"
                                )
                            }
                    )
                );
            }

            // use multisearch to retain active facets correctly (see also demo at https://songs-search.typesense.org/)
            var multiSearchParameters = new List<MultiSearchParameters>
            {
                // add the "main" document search, which performs all filtering to return the relevant documents.
                // this returns incorrect facet values for active facets.
                CreateMultiSearchParameters(
                    filtersAsArray,
                    FacetBy(facetsAsArray),
                    true
                )
            };

            // add "facet" searches for all active facets, in order to retrieve correct facet values for these.
            // to NOT retrieve documents for these searches - documents should only be retrieved by the "main" search.
            foreach (Filter facetFilter in facetFilters)
            {
                Filter[] effectiveFilters = filtersAsArray.Except([facetFilter]).ToArray();
                Facet effectiveFacet = facetsAsArray
                    .First(facet => facet.FieldName.InvariantEquals(facetFilter.FieldName));
                multiSearchParameters.Add(
                    CreateMultiSearchParameters(effectiveFilters, FacetBy([effectiveFacet]), false)
                );
            }

            List<MultiSearchResult<SearchResultDocument>> searchResults =
                await _typesenseClient.MultiSearch<SearchResultDocument>(multiSearchParameters);

            MultiSearchResult<SearchResultDocument>? searchResultError =
                searchResults.FirstOrDefault(r => r.ErrorCode.HasValue);
            if (searchResultError is not null)
            {
                _logger.LogWarning(
                    "Typesense could not execute the query. Error code: {errorCode}. Error message: {errorMessage}",
                    searchResultError.ErrorCode,
                    searchResultError.ErrorMessage ?? "n/a"
                );
                return new SearchResult(0, [], []);
            }

            // construct the correct facet values:
            // - the active facets returned by the "facet" searches
            // - all other facets from the "main" search
            var facetCounts = new List<FacetCount>();
            for (var i = searchResults.Count - 1; i >= 0; i--)
            {
                var knownFacetCounts = facetCounts.Select(c => c.FieldName).ToArray();
                facetCounts.AddRange(
                    searchResults[i].FacetCounts!.Where(c => knownFacetCounts.Contains(c.FieldName) is false)
                );
            }

            MultiSearchResult<SearchResultDocument> searchResponse = searchResults.First();

            Document[] documents = searchResponse.Hits!.Select(
                    hit => new Document(
                        hit.Document.Key,
                        Enum.TryParse(hit.Document.ObjectType, out UmbracoObjectTypes umbracoObjectType)
                            ? umbracoObjectType
                            : UmbracoObjectTypes.Unknown
                    )
                )
                .ToArray();

            FacetResult[] facetResults = facetsAsArray.Select(
                    facet =>
                    {
                        FacetCount? facetCount = facetCounts.FirstOrDefault(f => f.FieldName == FieldName(facet));
                        if (facetCount is null)
                        {
                            _logger.LogWarning(
                                "Typesense did not return facet values for facet: {facetName}.",
                                facet.FieldName
                            );
                            return null;
                        }

                        FacetResult facetResult = facet switch
                        {
                            KeywordFacet keywordFacet => new FacetResult(
                                keywordFacet.FieldName,
                                facetCount.Counts.Select(hit => new KeywordFacetValue(hit.Value, hit.Count))
                            ),
                            IntegerExactFacet integerExactFacet => new FacetResult(
                                integerExactFacet.FieldName,
                                facetCount.Counts.Select(
                                    hit => new IntegerExactFacetValue(int.Parse(hit.Value), hit.Count)
                                )
                            ),
                            DecimalExactFacet decimalExactFacet => new FacetResult(
                                decimalExactFacet.FieldName,
                                facetCount.Counts.Select(
                                    hit => new DecimalExactFacetValue(decimal.Parse(hit.Value, CultureInfo.InvariantCulture), hit.Count)
                                )
                            ),
                            DateTimeOffsetExactFacet dateTimeOffsetExactFacet => new FacetResult(
                                dateTimeOffsetExactFacet.FieldName,
                                facetCount.Counts.Select(
                                    hit => new DateTimeOffsetExactFacetValue(
                                        DateTimeOffset.FromUnixTimeSeconds(long.Parse(hit.Value)),
                                        hit.Count
                                    )
                                )
                            ),
                            IntegerRangeFacet integerRangeFacet => new FacetResult(
                                integerRangeFacet.FieldName,
                                integerRangeFacet.Ranges.Select(
                                    range => new IntegerRangeFacetValue(
                                        range.Key,
                                        range.Min,
                                        range.Max,
                                        facetCount.Counts.FirstOrDefault(count => count.Value == range.Key)?.Count ?? 0
                                    )
                                )
                            ),
                            DecimalRangeFacet decimalRangeFacet => new FacetResult(
                                decimalRangeFacet.FieldName,
                                decimalRangeFacet.Ranges.Select(
                                    range => new DecimalRangeFacetValue(
                                        range.Key,
                                        range.Min,
                                        range.Max,
                                        facetCount.Counts.FirstOrDefault(count => count.Value == range.Key)?.Count ?? 0
                                    )
                                )
                            ),
                            DateTimeOffsetRangeFacet dateTimeOffsetRangeFacet => new FacetResult(
                                dateTimeOffsetRangeFacet.FieldName,
                                dateTimeOffsetRangeFacet.Ranges.Select(
                                    range => new DateTimeOffsetRangeFacetValue(
                                        range.Key,
                                        range.Min,
                                        range.Max,
                                        facetCount.Counts.FirstOrDefault(count => count.Value == range.Key)?.Count ?? 0
                                    )
                                )
                            ),
                            _ => throw new ArgumentOutOfRangeException(
                                nameof(facet),
                                $"Encountered an unsupported facet type: {facet.GetType().Name}"
                            )
                        };

                        return facetResult;
                    }
                )
                .WhereNotNull()
                .ToArray();

            return new SearchResult(searchResponse.Found ?? 0, documents, facetResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to obtain a search response from Typesense.");
            return new SearchResult(0, [], []);
        }
    }

    private string FieldName(Filter filter)
        => filter switch
        {
            DateTimeOffsetExactFilter or DateTimeOffsetRangeFilter => FieldName(
                filter.FieldName,
                IndexConstants.FieldTypePostfix.DateTimeOffsets
            ),
            DecimalExactFilter or DecimalRangeFilter => FieldName(
                filter.FieldName,
                IndexConstants.FieldTypePostfix.Decimals
            ),
            IntegerExactFilter or IntegerRangeFilter => FieldName(
                filter.FieldName,
                IndexConstants.FieldTypePostfix.Integers
            ),
            KeywordFilter => FieldName(filter.FieldName, IndexConstants.FieldTypePostfix.Keywords),
            TextFilter => FieldName(filter.FieldName, IndexConstants.FieldTypePostfix.Texts),
            SystemFieldFilter => filter.FieldName,
            _ => throw new ArgumentOutOfRangeException(
                nameof(filter),
                $"Encountered an unsupported filter type: {filter.GetType().Name}"
            )
        };

    private string FieldName(Facet facet)
        => facet switch
        {
            IntegerExactFacet => FieldName(facet.FieldName, IndexConstants.FieldTypePostfix.Integers),
            IntegerRangeFacet => $"{FieldName(facet.FieldName, IndexConstants.FieldTypePostfix.Integers)}{IndexConstants.FieldTypePostfix.Sortable}",
            DecimalExactFacet => FieldName(facet.FieldName, IndexConstants.FieldTypePostfix.Decimals),
            DecimalRangeFacet => $"{FieldName(facet.FieldName, IndexConstants.FieldTypePostfix.Decimals)}{IndexConstants.FieldTypePostfix.Sortable}",
            DateTimeOffsetExactFacet => FieldName(facet.FieldName, IndexConstants.FieldTypePostfix.DateTimeOffsets),
            DateTimeOffsetRangeFacet => $"{FieldName(facet.FieldName, IndexConstants.FieldTypePostfix.DateTimeOffsets)}{IndexConstants.FieldTypePostfix.Sortable}",
            KeywordFacet => FieldName(facet.FieldName, IndexConstants.FieldTypePostfix.Keywords),
            _ => throw new ArgumentOutOfRangeException(
                nameof(facet),
                $"Encountered an unsupported facet type: {facet.GetType().Name}"
            )
        };

    private string FieldName(Sorter sorter)
        => sorter switch
        {
            DateTimeOffsetSorter => $"{FieldName(sorter.FieldName, IndexConstants.FieldTypePostfix.DateTimeOffsets)}{IndexConstants.FieldTypePostfix.Sortable}",
            DecimalSorter => $"{FieldName(sorter.FieldName, IndexConstants.FieldTypePostfix.Decimals)}{IndexConstants.FieldTypePostfix.Sortable}",
            IntegerSorter => $"{FieldName(sorter.FieldName, IndexConstants.FieldTypePostfix.Integers)}{IndexConstants.FieldTypePostfix.Sortable}",
            KeywordSorter => $"{FieldName(sorter.FieldName, IndexConstants.FieldTypePostfix.Keywords)}{IndexConstants.FieldTypePostfix.Sortable}",
            TextSorter => $"{FieldName(sorter.FieldName, IndexConstants.FieldTypePostfix.Texts)}{IndexConstants.FieldTypePostfix.Sortable}",
            ScoreSorter => "_text_match",
            _ => throw new ArgumentOutOfRangeException(
                nameof(sorter),
                $"Encountered an unsupported sorter type: {sorter.GetType().Name}"
            )
        };

    private static string DecimalValue(decimal value)
        => value.ToString("F2", CultureInfo.InvariantCulture);

    private static string? DecimalValue(decimal? value)
        => value.HasValue ? DecimalValue(value.Value) : null;

    private record SearchResultDocument
    {
        [JsonPropertyName(IndexConstants.FieldNames.Id)]
        public required string Id { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.Key)]
        public required Guid Key { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.ObjectType)]
        public required string ObjectType { get; init; }
    }
}
