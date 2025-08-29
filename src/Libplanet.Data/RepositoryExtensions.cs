using Libplanet.Types.Progresses;

namespace Libplanet.Data;

public static class RepositoryExtensions
{
    public static Task CopyToAsync(this Repository @this, Repository destination, CancellationToken cancellationToken)
        => @this.CopyToAsync(destination, cancellationToken, new Progress<ProgressInfo>());

    public static Task CopyToAsync(this Repository @this, Repository destination)
        => CopyToAsync(@this, destination, cancellationToken: default);
}
