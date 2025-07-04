using Umbraco.Cms.Core.Sync;

namespace Kjac.SearchProvider.Typesense.Services;

internal abstract class TypesenseIndexManagingServiceBase : TypesenseServiceBase
{
    private readonly IServerRoleAccessor _serverRoleAccessor;

    protected TypesenseIndexManagingServiceBase(IServerRoleAccessor serverRoleAccessor)
        => _serverRoleAccessor = serverRoleAccessor;

    protected bool ShouldNotManipulateIndexes() => _serverRoleAccessor.CurrentServerRole is ServerRole.Subscriber;
}
