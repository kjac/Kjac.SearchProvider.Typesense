using Umbraco.Cms.Search.Core.Services;

namespace Kjac.SearchProvider.Typesense.Services;

// public marker interface allowing for explicit index registrations using the Typesense searcher
public interface ITypesenseSearcher : ISearcher
{
}
