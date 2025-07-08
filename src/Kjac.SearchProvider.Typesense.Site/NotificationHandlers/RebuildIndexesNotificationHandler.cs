using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.Models.Configuration;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;

namespace Kjac.SearchProvider.Typesense.Site.NotificationHandlers;

public class RebuildIndexesNotificationHandler : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private readonly IContentIndexingService _contentIndexingService;
    private readonly IndexOptions _indexOptions;
    private readonly ILogger<RebuildIndexesNotificationHandler> _logger;

    public RebuildIndexesNotificationHandler(
        IContentIndexingService contentIndexingService,
        IOptions<IndexOptions> indexOptions,
        ILogger<RebuildIndexesNotificationHandler> logger)
    {
        _contentIndexingService = contentIndexingService;
        _indexOptions = indexOptions.Value;
        _logger = logger;
    }

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        _logger.LogInformation("Starting index rebuild");

        foreach (IndexRegistration indexRegistration in _indexOptions.GetIndexRegistrations())
        {
            _logger.LogInformation($"- Rebuilding {indexRegistration.IndexAlias}...");
            _contentIndexingService.Rebuild(indexRegistration.IndexAlias);
        }

        _logger.LogInformation("Index rebuild complete.");
    }
}
