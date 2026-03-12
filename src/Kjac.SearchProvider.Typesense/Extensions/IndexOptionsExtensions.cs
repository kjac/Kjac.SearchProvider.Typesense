using Kjac.SearchProvider.Typesense.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;

namespace Kjac.SearchProvider.Typesense.Extensions;

public static class IndexOptionsExtensions
{
    public static IndexOptions RegisterTypesenseContentIndex<TContentChangeStrategy>(
        this IndexOptions indexOptions,
        string indexAlias,
        params UmbracoObjectTypes[] containedObjectTypes)
        where TContentChangeStrategy : class, IContentChangeStrategy
    {
        indexOptions.RegisterContentIndex<ITypesenseIndexer, ITypesenseSearcher, TContentChangeStrategy>(
            indexAlias,
            sameOriginOnly: true,
            containedObjectTypes
        );
        return indexOptions;
    }
}
