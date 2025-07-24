namespace Libplanet.Net;

public sealed class FetchException(Guid requestId, string message, Exception innerException)
    : SystemException(message, innerException)
{
    public Guid RequestId { get; } = requestId;
}
