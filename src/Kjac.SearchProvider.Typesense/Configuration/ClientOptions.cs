namespace Kjac.SearchProvider.Typesense.Configuration;

public sealed class ClientOptions
{
    public Uri? Host { get; set; }

    public AuthenticationOptions? Authentication { get; set; }
}
