using Kjac.SearchProvider.Typesense.Constants;

namespace Kjac.SearchProvider.Typesense.Extensions;

internal static class StringExtensions
{
    public static string IndexCulture(this string? culture)
        => culture?.ToLowerInvariant()?? IndexConstants.Variation.InvariantCulture;

    public static string IndexSegment(this string? segment)
        => segment?.ToLowerInvariant() ?? IndexConstants.Variation.DefaultSegment;

    public static string ValidIndexAlias(this string indexAlias)
        => indexAlias;
}
