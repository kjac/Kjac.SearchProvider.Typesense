using Kjac.SearchProvider.Typesense.Constants;

namespace Kjac.SearchProvider.Typesense.Services;

internal abstract class TypesenseServiceBase
{
    protected string FieldName(string fieldName, string postfix)
        => $"{IndexConstants.FieldNames.Fields}{fieldName}{postfix}";
}
