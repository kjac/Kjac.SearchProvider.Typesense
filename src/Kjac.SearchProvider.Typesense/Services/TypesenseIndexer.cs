using System.Text.Json.Serialization;
using Kjac.SearchProvider.Typesense.Constants;
using Kjac.SearchProvider.Typesense.Extensions;
using Microsoft.Extensions.Logging;
using Typesense;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Extensions;
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
                var segment = variation.Segment.IndexSegment();

                // document access (no access maps to an empty key for querying)
                Guid[] accessKeys = protection?.AccessIds.Any() is true
                    ? protection.AccessIds.ToArray()
                    : [Guid.Empty];

                // relevant field values for this variation (including invariant fields)
                IndexField[] variationFields = fieldsByFieldName.Select(
                        g =>
                        {
                            IndexField[] applicableFields = g.Where(f =>
                                (variation.Culture is not null
                                 && variation.Segment is not null
                                 && f.Culture == variation.Culture
                                 && f.Segment == variation.Segment)
                                || (variation.Culture is not null
                                    && f.Culture == variation.Culture
                                    && f.Segment is null)
                                || (variation.Segment is not null
                                    && f.Culture is null
                                    && f.Segment == variation.Segment)
                                || (f.Culture is null && f.Segment is null)
                            ).ToArray();

                            return applicableFields.Any()
                                ? new IndexField(
                                    g.Key,
                                    new IndexValue
                                    {
                                        DateTimeOffsets = applicableFields.SelectMany(f => f.Value.DateTimeOffsets ?? []).NullIfEmpty(),
                                        Decimals = applicableFields.SelectMany(f => f.Value.Decimals ?? []).NullIfEmpty(),
                                        Integers = applicableFields.SelectMany(f => f.Value.Integers ?? []).NullIfEmpty(),
                                        Keywords = applicableFields.SelectMany(f => f.Value.Keywords ?? []).NullIfEmpty(),
                                        Texts = applicableFields.SelectMany(f => f.Value.Texts ?? []).NullIfEmpty(),
                                        TextsR1 = applicableFields.SelectMany(f => f.Value.TextsR1 ?? []).NullIfEmpty(),
                                        TextsR2 = applicableFields.SelectMany(f => f.Value.TextsR2 ?? []).NullIfEmpty(),
                                        TextsR3 = applicableFields.SelectMany(f => f.Value.TextsR3 ?? []).NullIfEmpty(),
                                    },
                                    variation.Culture,
                                    variation.Segment
                                )
                                : null;
                        }
                    )
                    .WhereNotNull()
                    .ToArray();

                // all text fields for "free text query on all fields"
                var allTexts = string.Join(
                    " ",
                    variationFields.SelectMany(field => field.Value.Texts ?? [])
                );
                var allTextsR1 = string.Join(
                    " ",
                    variationFields.SelectMany(field => field.Value.TextsR1 ?? [])
                );
                var allTextsR2 = string.Join(
                    " ",
                    variationFields.SelectMany(field => field.Value.TextsR2 ?? [])
                );
                var allTextsR3 = string.Join(
                    " ",
                    variationFields.SelectMany(field => field.Value.TextsR3 ?? [])
                );

                // explicit document field values
                var fieldValues = variationFields
                    .SelectMany(
                        field =>
                        {
                            // it is not possible to negate on a non-existing field, and we need to be able to negate
                            // on all textual relevance levels... so if one textual relevance field has a value, we
                            // must ensure that all textual relevance fields exist - even if that means indexing empty.
                            var defaultTextualRelevanceFieldValue = field.Value.Texts?.Any() is true
                                                                    || field.Value.TextsR1?.Any() is true
                                                                    || field.Value.TextsR2?.Any() is true
                                                                    || field.Value.TextsR3?.Any() is true
                                ? Array.Empty<object>()
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

                    if (field.Value.TextsR1 is not null)
                    {
                        fieldValues.Add(
                            FieldName(
                                field.FieldName,
                                $"{IndexConstants.FieldTypePostfix.TextsR1}{IndexConstants.FieldTypePostfix.Sortable}"
                            ),
                            field.Value.TextsR1.First()
                        );
                    }

                    if (field.Value.TextsR2 is not null)
                    {
                        fieldValues.Add(
                            FieldName(
                                field.FieldName,
                                $"{IndexConstants.FieldTypePostfix.TextsR2}{IndexConstants.FieldTypePostfix.Sortable}"
                            ),
                            field.Value.TextsR2.First()
                        );
                    }

                    if (field.Value.TextsR3 is not null)
                    {
                        fieldValues.Add(
                            FieldName(
                                field.FieldName,
                                $"{IndexConstants.FieldTypePostfix.TextsR3}{IndexConstants.FieldTypePostfix.Sortable}"
                            ),
                            field.Value.TextsR3.First()
                        );
                    }

                    if (field.Value.Texts is not null)
                    {
                        fieldValues.Add(
                            FieldName(
                                field.FieldName,
                                $"{IndexConstants.FieldTypePostfix.Texts}{IndexConstants.FieldTypePostfix.Sortable}"
                            ),
                            field.Value.Texts.First()
                        );
                    }
                }

                return new IndexDocument
                {
                    Id = $"{id:D}.{culture}.{segment}",
                    ObjectType = objectType.ToString(),
                    Key = id,
                    Culture = culture,
                    Segment = segment,
                    AccessKeys = accessKeys,
                    AllTexts = allTexts,
                    AllTextsR1 = allTextsR1,
                    AllTextsR2 = allTextsR2,
                    AllTextsR3 = allTextsR3,
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

        [JsonPropertyName(IndexConstants.FieldNames.Segment)]
        public required string Segment { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.AccessKeys)]
        public required Guid[] AccessKeys { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.AllTexts)]
        public required string AllTexts { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.AllTextsR1)]
        public required string AllTextsR1 { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.AllTextsR2)]
        public required string AllTextsR2 { get; init; }

        [JsonPropertyName(IndexConstants.FieldNames.AllTextsR3)]
        public required string AllTextsR3 { get; init; }

        [JsonExtensionData]
        public Dictionary<string, object> Fields { get; init; } = new();
    }
}
