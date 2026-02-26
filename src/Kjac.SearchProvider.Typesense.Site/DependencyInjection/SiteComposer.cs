using Kjac.SearchProvider.Typesense.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Search.Core.DependencyInjection;

namespace Kjac.SearchProvider.Typesense.Site.DependencyInjection;

public class SiteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder
            // add core services for search abstractions
            .AddSearchCore()
            // use the Typesense search provider
            .AddTypesenseSearchProvider();

        // configure System.Text.Json to allow serializing output models
        builder.ConfigureJsonOptions();
    }
}
