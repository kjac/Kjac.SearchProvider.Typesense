using Kjac.SearchProvider.Typesense.NotificationHandlers;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Kjac.SearchProvider.Typesense.Services;
using CoreConstants = Umbraco.Cms.Search.Core.Constants;

namespace Kjac.SearchProvider.Typesense.DependencyInjection;

public static class UmbracoBuilderExtensions
{
    public static IUmbracoBuilder AddTypesenseSearchProvider(this IUmbracoBuilder builder)
    {
        builder.Services.AddTypesense(builder.Config);

        builder.Services.Configure<IndexOptions>(
            options =>
            {
                // register Typesense indexes for draft and published content
                options.RegisterIndex<ITypesenseIndexer, ITypesenseSearcher, IDraftContentChangeStrategy>(
                    CoreConstants.IndexAliases.DraftContent,
                    UmbracoObjectTypes.Document
                );
                options.RegisterIndex<ITypesenseIndexer, ITypesenseSearcher, IPublishedContentChangeStrategy>(
                    CoreConstants.IndexAliases.PublishedContent,
                    UmbracoObjectTypes.Document
                );

                // register Typesense index for media
                options.RegisterIndex<ITypesenseIndexer, ITypesenseSearcher, IDraftContentChangeStrategy>(
                    CoreConstants.IndexAliases.DraftMedia,
                    UmbracoObjectTypes.Media
                );

                // register Typesense index for members
                options.RegisterIndex<ITypesenseIndexer, ITypesenseSearcher, IDraftContentChangeStrategy>(
                    CoreConstants.IndexAliases.DraftMembers,
                    UmbracoObjectTypes.Member
                );
            }
        );

        // ensure all indexes exist before Umbraco has finished start-up
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, EnsureIndexesNotificationHandler>();

        return builder;
    }
}
