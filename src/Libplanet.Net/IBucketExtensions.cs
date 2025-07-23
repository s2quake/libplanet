using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net;

internal static class IBucketExtensions
{
    private static readonly Random _random = new();

    public static Peer GetRandomPeer(this IBucket @this, Address except)
    {
        var query = from item in @this
                    where item.Address != except
                    orderby _random.Next()
                    select item;

        try
        {
            return query.First().Peer;
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidOperationException($"No peer found in the bucket except {except}.", e);
        }
    }

    public static bool TryGetRandomPeer(this IBucket @this, Address except, [MaybeNullWhen(false)] out Peer value)
    {
        var query = from item in @this
                    where item.Address != except
                    orderby _random.Next()
                    select item;

        if (query.FirstOrDefault() is { } peerState)
        {
            value = peerState.Peer;
            return true;
        }

        value = default;
        return false;
    }

}
