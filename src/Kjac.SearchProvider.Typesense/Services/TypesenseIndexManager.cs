using Kjac.SearchProvider.Typesense.Configuration;
using Kjac.SearchProvider.Typesense.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Typesense;
using Umbraco.Cms.Core.Sync;
using CoreConstants = Umbraco.Cms.Search.Core.Constants;

namespace Kjac.SearchProvider.Typesense.Services;

internal sealed class TypesenseIndexManager : TypesenseIndexManagingServiceBase, ITypesenseIndexManager
{
    private readonly ITypesenseClient _typesenseClient;
    private readonly IndexerOptions _indexerOptions;
    private readonly IIndexAliasResolver _indexAliasResolver;
    private readonly ILogger<TypesenseIndexManager> _logger;

    public TypesenseIndexManager(
        IServerRoleAccessor serverRoleAccessor,
        ITypesenseClient typesenseClient,
        IOptions<IndexerOptions> options,
        IIndexAliasResolver indexAliasResolver,
        ILogger<TypesenseIndexManager> logger)
        : base(serverRoleAccessor)
    {
        _typesenseClient = typesenseClient;
        _indexerOptions = options.Value;
        _indexAliasResolver = indexAliasResolver;
        _logger = logger;
    }

    public async Task EnsureAsync(string indexAlias)
    {
        if (ShouldNotManipulateIndexes())
        {
            return;
        }

        indexAlias = _indexAliasResolver.Resolve(indexAlias);
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
                        // workaround for https://github.com/typesense/typesense/issues/2434
                        new(
                            FieldName(CoreConstants.FieldNames.PathIds, IndexConstants.FieldTypePostfix.Keywords),
                            FieldType.StringArray
                        ) { Facet = true, Store = true},
                        // system fields from Typesense
                        new(IndexConstants.FieldNames.Key, FieldType.String) { Index = false },
                        new(IndexConstants.FieldNames.ObjectType, FieldType.String) { Index = false },
                        new(IndexConstants.FieldNames.Culture, FieldType.String) { Store = _indexerOptions.StoreFields },
                        new($"{IndexConstants.FieldNames.AllTextsPrefix}{IndexConstants.FieldNames.AllTextsR1}", FieldType.String) { Sort = true, Store = _indexerOptions.StoreFields, Optional = true },
                        new($"{IndexConstants.FieldNames.AllTextsPrefix}{IndexConstants.FieldNames.AllTextsR2}", FieldType.String) { Sort = true, Store = _indexerOptions.StoreFields, Optional = true },
                        new($"{IndexConstants.FieldNames.AllTextsPrefix}{IndexConstants.FieldNames.AllTextsR3}", FieldType.String) { Sort = true, Store = _indexerOptions.StoreFields, Optional = true },
                        new($"{IndexConstants.FieldNames.AllTextsPrefix}{IndexConstants.FieldNames.AllTexts}", FieldType.String) { Sort = true, Store = _indexerOptions.StoreFields, Optional = true },
                        new($"{IndexConstants.FieldNames.AllTextsPrefix}_.*", FieldType.String) { Sort = true, Store = _indexerOptions.StoreFields, Optional = true },
                        // property value fields
                        // NOTE: the "Sortable" fields are used both for sorting and for range facets,
                        //       so they need to be declared as both facet-able and sortable
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.Keywords}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.String
                        ) { Facet = true, Sort = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.Keywords}",
                            FieldType.StringArray
                        ) { Facet = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.Integers}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.Float
                        ) { Facet = true, Sort = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.Integers}",
                            FieldType.FloatArray
                        ) { Facet = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.Decimals}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.Float
                        ) { Facet = true, Sort = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.Decimals}",
                            FieldType.FloatArray
                        ) { Facet = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.DateTimeOffsets}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.Int64
                        ) { Facet = true, Sort = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.DateTimeOffsets}",
                            FieldType.Int64Array
                        ) { Facet = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.Texts}{IndexConstants.FieldTypePostfix.Sortable}",
                            FieldType.String
                        ) { Facet = true, Sort = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.TextsR1}",
                            FieldType.StringArray
                        ) { Facet = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.TextsR2}",
                            FieldType.StringArray
                        ) { Facet = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.TextsR3}",
                            FieldType.StringArray
                        ) { Facet = true, Store = _indexerOptions.StoreFields },
                        new(
                            $"{IndexConstants.FieldNames.FieldsPrefix}.*{IndexConstants.FieldTypePostfix.Texts}",
                            FieldType.StringArray
                        ) { Facet = true, Store = _indexerOptions.StoreFields },
                        new(".*", FieldType.Auto) { Store = _indexerOptions.StoreFields }
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

    public async Task ResetAsync(string indexAlias)
    {
        if (ShouldNotManipulateIndexes())
        {
            return;
        }

        indexAlias = _indexAliasResolver.Resolve(indexAlias);

        try
        {
            await _typesenseClient.DeleteCollection(indexAlias);
        }
        catch (TypesenseApiNotFoundException)
        {
            // the index does not exist
        }
        catch (TypesenseApiException ex)
        {
            _logger.LogError(ex, "Index {indexAlias} could not be deleted.", indexAlias);
            return;
        }

        await EnsureAsync(indexAlias);
    }
}
