using Kjac.SearchProvider.Typesense.Constants;

namespace Kjac.SearchProvider.Typesense.Extensions;

internal static class StringExtensions
{
    public static string IndexCulture(this string? culture)
        => culture?.ToLowerInvariant() ?? IndexConstants.Variation.InvariantCulture;
}
