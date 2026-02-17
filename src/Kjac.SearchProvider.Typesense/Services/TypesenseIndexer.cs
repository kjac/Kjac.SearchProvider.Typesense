using System.Text.Json.Serialization;
using Kjac.SearchProvider.Typesense.Constants;
using Kjac.SearchProvider.Typesense.Extensions;
using Microsoft.Extensions.Logging;
using Typesense;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Indexing;
using CoreConstants = Umbraco.Cms.Search.Core.Constants;

namespace Kjac.SearchProvider.Typesense.Services;

internal sealed class TypesenseIndexer : TypesenseIndexManagingServiceBase, ITypesenseIndexer
{
    private readonly ITypesenseClient _typesenseClient;
    private readonly ITypesenseIndexManager _indexManager;
    private readonly IIndexAliasResolver _indexAliasResolver;
    private readonly ILogger<TypesenseIndexer> _logger;

    public TypesenseIndexer(
        IServerRoleAccessor serverRoleAccessor,
        ITypesenseClient typesenseClient,
        ITypesenseIndexManager indexManager,
        IIndexAliasResolver indexAliasResolver,
        ILogger<TypesenseIndexer> logger)
        : base(serverRoleAccessor)
    {
        _typesenseClient = typesenseClient;
        _indexManager = indexManager;
        _indexAliasResolver = indexAliasResolver;
        _logger = logger;
    }

    public async Task AddOrUpdateAsync(
        string indexAlias,
        Guid id,
        UmbracoObjectTypes objectType,
        IEnumerable<Variation> variations,
        IEnumerable<IndexField> fields,
        ContentProtection? protection)
    {
        if (ShouldNotManipulateIndexes())
        {
            return;
        }

        IEnumerable<IGrouping<string, IndexField>> fieldsByFieldName = fields.GroupBy(field => field.FieldName);
        IEnumerable<IndexDocument> documents = variations.Select(
            variation =>
            {
                // document variation
                var culture = variation.Culture.IndexCulture();

                // document access (no access maps to an empty key for querying)
                Guid[] accessKeys = protection?.AccessIds.Any() is true
                    ? protection.AccessIds.ToArray()
                    : [Guid.Empty];

                // relevant field values for this variation (including invariant fields)
                IndexField[] variationFields = fieldsByFieldName.SelectMany(
                        g =>
                        {
                            IndexField[] applicableFields = g
                                .Where(f => f.Culture is null || f.Culture == variation.Culture)
                                .ToArray();

                            return applicableFields.Any()
                                ? applicableFields
                                    .GroupBy(field => field.Segment)
                                    .Select(segmentFields => new IndexField(
                                        SegmentedField(g.Key, segmentFields.Key),
                                        new IndexValue
                                        {
                                            DateTimeOffsets = segmentFields.SelectMany(f => f.Value.DateTimeOffsets ?? []).NullIfEmpty(),
                                            Decimals = segmentFields.SelectMany(f => f.Value.Decimals ?? []).NullIfEmpty(),
                                            Integers = segmentFields.SelectMany(f => f.Value.Integers ?? []).NullIfEmpty(),
                                            Keywords = segmentFields.SelectMany(f => f.Value.Keywords ?? []).NullIfEmpty(),
                                            Texts = segmentFields.SelectMany(f => f.Value.Texts ?? []).NullIfEmpty(),
                                            TextsR1 = segmentFields.SelectMany(f => f.Value.TextsR1 ?? []).NullIfEmpty(),
                                            TextsR2 = segmentFields.SelectMany(f => f.Value.TextsR2 ?? []).NullIfEmpty(),
                                            TextsR3 = segmentFields.SelectMany(f => f.Value.TextsR3 ?? []).NullIfEmpty(),
                                        },
                                        variation.Culture,
                                        segmentFields.Key
                                    ))
                                : [];
                        }
                    )
                    .ToArray();

                // explicit document field values
                var fieldValues = variationFields
                    .SelectMany(
                        field =>
                        {
                            // it is not possible to negate on a non-existing field, and we need to be able to negate
                            // on all textual relevance levels... so if one textual relevance field has a value, we
                            // must ensure that all textual relevance fields exist - even if that means indexing empty.
                            object[]? defaultTextualRelevanceFieldValue = field.Value.Texts?.Any() is true
                                                                          || field.Value.TextsR1?.Any() is true
                                                                          || field.Value.TextsR2?.Any() is true
                                                                          || field.Value.TextsR3?.Any() is true
                                ? []
                                : null;

                            object[]? TextualRelevanceFieldValue(IEnumerable<string>? value)
                                => value?.NullIfEmpty()?.OfType<object>().ToArray() ?? defaultTextualRelevanceFieldValue;

                            return new (string FieldName, string Postfix, object[]? Values)[]
                            {
                                (
                                    field.FieldName,
                                    IndexConstants.FieldTypePostfix.Texts,
                                    TextualRelevanceFieldValue(field.Value.Texts)
                                ),
                                (
                                    field.FieldName,
                                    IndexConstants.FieldTypePostfix.TextsR1,
                                    TextualRelevanceFieldValue(field.Value.TextsR1)
                                ),
                                (
                                    field.FieldName,
                                    IndexConstants.FieldTypePostfix.TextsR2,
                                    TextualRelevanceFieldValue(field.Value.TextsR2)
                                ),
                                (
                                    field.FieldName,
                                    IndexConstants.FieldTypePostfix.TextsR3,
                                    TextualRelevanceFieldValue(field.Value.TextsR3)
                                ),
                                (
                                    field.FieldName,
                                    IndexConstants.FieldTypePostfix.Integers,
                                    field.Value.Integers?.OfType<object>().ToArray()
                                ),
                                (
                                    field.FieldName,
                                    IndexConstants.FieldTypePostfix.Decimals,
                                    field.Value.Decimals?.OfType<object>().ToArray()
                                ),
                                (
                                    field.FieldName,
                                    IndexConstants.FieldTypePostfix.DateTimeOffsets,
                                    field.Value.DateTimeOffsets?
                                        // Typesense expects unix timestamps for dates
                                        .Select(dt => dt.ToUnixTimeSeconds())
                                        .OfType<object>()
                                        .ToArray()
                                ),
                                (
                                    field.FieldName,
                                    IndexConstants.FieldTypePostfix.Keywords,
                                    field.Value.Keywords?.OfType<object>().ToArray()
                                )
                            };
                        }
                    )
                    .Where(f => f.Values is not null)
                    .ToDictionary(f => FieldName(f.FieldName, f.Postfix), object (f) => f.Values!);

                IndexField[] textFields = variationFields
                    .Where(f => f.Value.Texts is not null
                                || f.Value.TextsR1 is not null
                                || f.Value.TextsR2 is not null
                                || f.Value.TextsR3 is not null
                    )
                    .ToArray();

                // all text fields for "free text query on all fields"
                IndexField[] defaultSegmentTextFields = textFields.Where(f => f.Segment is null).ToArray();
                var allTexts = defaultSegmentTextFields
                    .SelectMany(field => field.Value.Texts ?? [])
                    .ToArray();
                var allTextsR1 = defaultSegmentTextFields
                    .SelectMany(field => field.Value.TextsR1 ?? [])
                    .ToArray();
                var allTextsR2 = defaultSegmentTextFields
                    .SelectMany(field => field.Value.TextsR2 ?? [])
                    .ToArray();
                var allTextsR3 = defaultSegmentTextFields
                    .SelectMany(field => field.Value.TextsR3 ?? [])
                    .ToArray();

                if (allTexts.Length > 0)
                {
                    fieldValues.Add(AllTextsFieldName(IndexConstants.FieldNames.AllTexts, null), string.Join(" ", allTexts).ToLowerInvariant());
                }

                if (allTextsR1.Length > 0)
                {
                    fieldValues.Add(AllTextsFieldName(IndexConstants.FieldNames.AllTextsR1, null), string.Join(" ", allTextsR1).ToLowerInvariant());
                }

                if (allTextsR2.Length > 0)
                {
                    fieldValues.Add(AllTextsFieldName(IndexConstants.FieldNames.AllTextsR2, null), string.Join(" ", allTextsR2).ToLowerInvariant());
                }

                if (allTextsR3.Length > 0)
                {
                    fieldValues.Add(AllTextsFieldName(IndexConstants.FieldNames.AllTextsR3, null), string.Join(" ", allTextsR3).ToLowerInvariant());
                }

                // all text fields for "free text query on all fields" (segment values)
                foreach (IGrouping<string?, IndexField> textFieldsBySegment in textFields.Except(defaultSegmentTextFields).GroupBy(f => f.Segment))
                {
                    var allTextsForSegment = textFieldsBySegment
                        .SelectMany(field => field.Value.Texts ?? [])
                        .ToArray();
                    var allTextsR1ForSegment = textFieldsBySegment
                        .SelectMany(field => field.Value.TextsR1 ?? [])
                        .ToArray();
                    var allTextsR2ForSegment = textFieldsBySegment
                        .SelectMany(field => field.Value.TextsR2 ?? [])
                        .ToArray();
                    var allTextsR3ForSegment = textFieldsBySegment
                        .SelectMany(field => field.Value.TextsR3 ?? [])
                        .ToArray();

                    if (allTextsForSegment.Length > 0)
                    {
                        fieldValues.Add(
                            AllTextsFieldName(IndexConstants.FieldNames.AllTexts, textFieldsBySegment.Key),
                            string.Join(" ", allTextsForSegment.Union(allTexts)).ToLowerInvariant()
                        );
                    }

                    if (allTextsR1.Length > 0)
                    {
                        fieldValues.Add(
                            AllTextsFieldName(IndexConstants.FieldNames.AllTextsR1, textFieldsBySegment.Key),
                            string.Join(" ", allTextsR1ForSegment.Union(allTextsR1)).ToLowerInvariant()
                        );
                    }

                    if (allTextsR2.Length > 0)
                    {
                        fieldValues.Add(
                            AllTextsFieldName(IndexConstants.FieldNames.AllTextsR2, textFieldsBySegment.Key),
                            string.Join(" ", allTextsR2ForSegment.Union(allTexts)).ToLowerInvariant()
                        );
                    }

                    if (allTextsR3.Length > 0)
                    {
                        fieldValues.Add(
                            AllTextsFieldName(IndexConstants.FieldNames.AllTextsR3, textFieldsBySegment.Key),
                            string.Join(" ", allTextsR3ForSegment.Union(allTextsR3)).ToLowerInvariant()
                        );
                    }
                }

                // add explicit fields for range facets
                foreach (IndexField field in variationFields)
                {
                    if (field.Value.Keywords?.Any() is true)
                    {
                        fieldValues.Add(
                            FieldName(
                                field.FieldName,
                                $"{IndexConstants.FieldTypePostfix.Keywords}{IndexConstants.FieldTypePostfix.Sortable}"
                            ),
                            field.Value.Keywords.First()
                        );
                    }

                    if (field.Value.Integers?.Any() is true)
                    {
                        fieldValues.Add(
                            FieldName(
                                field.FieldName,
                                $"{IndexConstants.FieldTypePostfix.Integers}{IndexConstants.FieldTypePostfix.Sortable}"
                            ),
                            field.Value.Integers.First()
                        );
                    }

                    if (field.Value.Decimals?.Any() is true)
                    {
                        fieldValues.Add(
                            FieldName(
                                field.FieldName,
                                $"{IndexConstants.FieldTypePostfix.Decimals}{IndexConstants.FieldTypePostfix.Sortable}"
                            ),
                            field.Value.Decimals.First()
                        );
                    }

                    if (field.Value.DateTimeOffsets?.Any() is true)
                    {
                        fieldValues.Add(
                            FieldName(
                                field.FieldName,
                                $"{IndexConstants.FieldTypePostfix.DateTimeOffsets}{IndexConstants.FieldTypePostfix.Sortable}"
                            ),
                            field.Value.DateTimeOffsets.First().ToUnixTimeSeconds()
                        );
                    }

                    // textual sorting is only ever performed for FieldTypePostfix.Texts - calculate appropriate sorting here from all textual relevance fields
                    var sortableTexts = (field.Value.TextsR1 ?? [])
                        .Union(field.Value.TextsR2 ?? [])
                        .Union(field.Value.TextsR3 ?? [])
                        .Union(field.Value.Texts ?? [])
                        .Take(5).ToArray();
                    if (sortableTexts.Length > 0)
                    {
                        fieldValues.Add(
                            FieldName(
                                field.FieldName,
                                $"{IndexConstants.FieldTypePostfix.Texts}{IndexConstants.FieldTypePostfix.Sortable}"
                            ),
                            string.Join(" ", sortableTexts).ToLowerInvariant()
                        );
                    }
                }

                return new IndexDocument
                {
                    Id = $"{id:D}.{culture}",
                    ObjectType = objectType.ToString(),
                    Key = id,
                    Culture = culture,
                    AccessKeys = accessKeys,
                    Fields = fieldValues
                };
            }
        );

        try
        {
            List<ImportResponse> importDocumentResults = await _typesenseClient.ImportDocuments(
                 _indexAliasResolver.Resolve(indexAlias),
                documents,
                importType: ImportType.Upsert
            );

            ImportResponse[] unsuccessful = importDocumentResults.Where(r => r.Success is false).ToArray();
            if (unsuccessful.Length > 0)
            {
                var errorDetails = unsuccessful.FirstOrDefault(r => r.Error is not null)?.Error ?? "n/a";
                _logger.LogWarning(
                    "One or more documents were not indexed by Typesense. First error returned was: {firstErrorMessage}",
                    errorDetails
                );
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to perform indexing operation against Typesense.");
        }
    }

    public async Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids)
    {
        if (ShouldNotManipulateIndexes())
        {
            return;
        }

        await _typesenseClient.DeleteDocuments(
            _indexAliasResolver.Resolve(indexAlias),
            $"{FieldName(CoreConstants.FieldNames.PathIds, IndexConstants.FieldTypePostfix.Keywords)}:[{string.Join(",", ids.Select(id => $"`{id.AsKeyword()}`"))}]"
        );
    }

    public async Task ResetAsync(string indexAlias)
        => await _indexManager.ResetAsync(indexAlias);

    public async Task<IndexMetadata> GetMetadataAsync(string indexAlias)
    {
        try
        {
            CollectionResponse collectionResponse = await _typesenseClient.RetrieveCollection(_indexAliasResolver.Resolve(indexAlias));
            return new IndexMetadata(
                collectionResponse.NumberOfDocuments,
                collectionResponse.NumberOfDocuments == 0
                    ? HealthStatus.Empty
                    : HealthStatus.Healthy
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to fetch the collection info for alias: {indexAlias}", indexAlias);
            return new IndexMetadata(0, HealthStatus.Unknown);
        }
    }

    private record IndexDocument
    {
        [JsonPropertyName(IndexConstants.FieldNames.Id)]
        public required string Id { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.ObjectType)]
        public required string? ObjectType { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.Key)]
        public required Guid Key { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.Culture)]
        public required string Culture { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.AccessKeys)]
        public required Guid[] AccessKeys { get; init; }

        [JsonExtensionData]
        public Dictionary<string, object> Fields { get; init; } = new();
    }
}
