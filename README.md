# Umbraco search provider for Typesense

This repo contains an alternative search provider for [Umbraco search](https://TODO), based on [Typesense](https://typesense.org/).

## Prerequisites

An Typesense engine must be available and running 😛

## Installation

The package is installed from [NuGet](https://www.nuget.org/packages/Kjac.SearchProvider.Typesense):

```bash
dotnet add package Kjac.SearchProvider.Typesense
```

Once installed, add the search provider to Umbraco by means of composition:

```csharp
using Kjac.SearchProvider.Typesense.DependencyInjection;
using Umbraco.Cms.Core.Composing;

namespace My.Site;

public class SiteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddTypesenseSearchProvider();
}
```

## Connecting to Typesense

You'll need to configure the search provider, so it can connect to your Typesense engine. This is done via `appsettings.json`:

```json
{
  "TypesenseSearchProvider": {
    "Client": {
      "Host": "[your Typesense host]",
      "Authentication": {
        "ApiKey": "[your Typesense API key]"
      }
    }
  }
}
```

## Extendability

Generally, you should look to Umbraco search for extension points. There are however a few notable extension points in this search provider as well.

### Tweaking score boosting for textual relevance

TODO: VERIFY DEFAULTS

Umbraco search allows for multiple textual relevance options within a single field. You can change the boost factors of the search provider by configuring the [`SearcherOptions`](https://github.com/kjac/Kjac.SearchProvider.Typesense/blob/main/src/Kjac.SearchProvider.Typesense/Configuration/SearcherOptions.cs):

```csharp
builder.Services.Configure<SearcherOptions>(options =>
{
    // boost the highest relevance text by a factor 100 (default is 6)
    options.BoostFactorTextR1 = 100;
    // boost the second-highest relevance text by a factor 10 (default is 4)
    options.BoostFactorTextR2 = 10;
    // do not boost the third-highest relevance text at all (default is 2)
    options.BoostFactorTextR3 = 1;
});
```

### Allowing for more facet values

TODO: VERIFY DEFAULTS

By default, the search provider allows for a maximum of 100 facet values returned per facet in a search result. You can change that - also using `SearcherOptions`:

```csharp
builder.Services.Configure<SearcherOptions>(options =>
{
    // allow fetching 200 facet values per facet
    options.MaxFacetValues = 200;[README.md](src/Kjac.SearchProvider.Typesense.Tests/README.md)
});
```

> [!IMPORTANT]
> Increasing the maximum number of facet values per facet can degrade your overall search performance. Use with caution.

## Contributing

Yes, please ❤️

When raising an issue, please make sure to include plenty of context, steps to reproduce and any other relevant information in the issue description 🥺 

If you're submitting a PR, please:

1. Also include plenty of context and steps to reproduce.
2. Make sure your code follows the provided editor configuration.
3. If at all possible, create tests that prove the issue has been fixed.
   - You'll find instructions on running the tests [here](https://TODO).
