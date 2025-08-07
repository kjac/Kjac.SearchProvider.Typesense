namespace Kjac.SearchProvider.Typesense.Services;

public interface IIndexAliasResolver
{
    string Resolve(string indexAlias);
}
