using Kjac.SearchProvider.Typesense.Constants;
using Kjac.SearchProvider.Typesense.Extensions;
using Microsoft.Extensions.Logging;
using Typesense;
using Umbraco.Cms.Core.Sync;

namespace Kjac.SearchProvider.Typesense.Services;

// TODO: need to support environments? perhaps an option to prefix indexes with an environment identifier?
internal sealed class TypesenseIndexManager : TypesenseIndexManagingServiceBase, ITypesenseIndexManager
{
    private readonly ITypesenseClient _typesenseClient;
    private readonly ILogger<TypesenseIndexManager> _logger;

    public TypesenseIndexManager(
        IServerRoleAccessor serverRoleAccessor,
        ITypesenseClient typesenseClient,
        ILogger<TypesenseIndexManager> logger)
        : base(serverRoleAccessor)
    {
        _typesenseClient = typesenseClient;
        _logger = logger;
    }

    public async Task EnsureAsync(string indexAlias)
    {
        if (ShouldNotManipulateIndexes())
        {
            return;
        }

        indexAlias = indexAlias.ValidIndexAlias();
        try
        {
            await _typesenseClient.RetrieveCollection(indexAlias);
            return;
        }
        catch (TypesenseApiNotFoundException)
        {
            // the index does not exist
        }

        try
        {
            await _typesenseClient.CreateCollection(
                new Schema(
                    indexAlias,
                    [
                        new(IndexConstants.FieldNames.Key, FieldType.String) { Index = false },
                        new(IndexConstants.FieldNames.ObjectType, FieldType.String) { Index = false },
                        new(IndexConstants.FieldNames.Culture, FieldType.String) { Store = false },
                        new(IndexConstants.FieldNames.Segment, FieldType.String) { Store = false },
                        new(IndexConstants.FieldNames.AllTextsR1, FieldType.String) { Sort = true, Store = false },
                        new(IndexConstants.FieldNames.AllTextsR2, FieldType.String) { Sort = true, Store = false },
                        new(IndexConstants.FieldNames.AllTextsR3, FieldType.String) { Sort = true, Store = false },
                        new(IndexConstants.FieldNames.AllTexts, FieldType.String) { Sort = true },
                        // NOTe: the "Sortable" fields are used both for sorting and for range facets,
                        //       so they need to be declared as both facet-able and sortable
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.Keywords}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.String
                        ) { Facet = true, Sort = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.Keywords}",
                            FieldType.StringArray
                        ) { Facet = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.Integers}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.Float
                        ) { Facet = true, Sort = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.Integers}",
                            FieldType.FloatArray
                        ) { Facet = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.Decimals}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.Float
                        ) { Facet = true, Sort = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.Decimals}",
                            FieldType.FloatArray
                        ) { Facet = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.DateTimeOffsets}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.Int64
                        ) { Facet = true, Sort = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.DateTimeOffsets}",
                            FieldType.Int64Array
                        ) { Facet = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.Texts}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.String
                        ) { Facet = true, Sort = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.TextsR1}",
                            FieldType.StringArray
                        ) { Facet = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.TextsR2}",
                            FieldType.StringArray
                        ) { Facet = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.TextsR3}",
                            FieldType.StringArray
                        ) { Facet = true, Store = false },
                        new(
                            $"{IndexConstants.FieldNames.Fields}.*{IndexConstants.FieldTypePostfix.Texts}",
                            FieldType.StringArray
                        ) { Facet = true, Store = false },
                        new(".*", FieldType.Auto) { Store = false }
                    ]
                )
            );

            _logger.LogInformation("Index {indexAlias} has been created.", indexAlias);
        }
        catch (TypesenseApiException ex)
        {
            _logger.LogError(ex, "Index {indexAlias} could not be created.", indexAlias);
        }
    }
}
