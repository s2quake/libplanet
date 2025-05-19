using Libplanet.Store;

namespace Libplanet.Node.Services;

public interface IStoreService
{
    Repository Repository { get; }
}
