namespace Kjac.SearchProvider.Typesense.Services;

internal interface ITypesenseIndexManager
{
    Task EnsureAsync(string indexAlias);
}
