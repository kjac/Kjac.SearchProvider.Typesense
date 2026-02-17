using Kjac.SearchProvider.Typesense.Constants;
using Umbraco.Extensions;

namespace Kjac.SearchProvider.Typesense.Services;

internal abstract class TypesenseServiceBase
{
    protected static string FieldName(string fieldName, string postfix, string? segment = null)
        => $"{IndexConstants.FieldNames.FieldsPrefix}{SegmentedField(fieldName, segment)}{postfix}";

    protected static string SegmentedField(string fieldName, string? segment)
        => segment.IsNullOrWhiteSpace() ? fieldName : $"__{segment}_{fieldName}";

    protected static string AllTextsFieldName(string field, string? segment)
        => $"{IndexConstants.FieldNames.AllTextsPrefix}{SegmentedField(field, segment)}";
}
