using Umbraco.Cms.Search.Core.Models.Searching.Filtering;

namespace Kjac.SearchProvider.Typesense.Models.Searching;

internal record SystemFieldFilter(string FieldName, string Value, bool Negate) : Filter(FieldName, Negate)
{
}
