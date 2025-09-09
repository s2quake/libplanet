#pragma warning disable SA1402 // File may only contain a single type
using System.ComponentModel.DataAnnotations;

namespace Libplanet.Node.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class PeerAttribute : RegularExpressionAttribute
{
    public PeerAttribute()
        : base(GetPattern())
    {
    }

    private static string GetPattern()
    {
        var items = new string[]
        {
            AddressAttribute.OriginPattern,
            DnsEndPointAttribute.HostPattern,
            DnsEndPointAttribute.PortPattern,
        };
        var pattern = string.Join(",", items);
        return @$"^$|^{pattern}$";
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class BoundPeerArrayAttribute : ArrayAttribute<PeerAttribute>
{
}
