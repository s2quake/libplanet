using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class ProtocolBuilder
{
    public int Version { get; init; }

    public ImmutableSortedDictionary<string, object> Properties { get; init; }
        = ImmutableSortedDictionary<string, object>.Empty;

    public ProtocolBuilder AddProperty(string key, object value)
        => this with { Properties = Properties.Add(key, value) };

    public Protocol Create(ISigner signer)
    {
        var metadata = new ProtocolMetadata
        {
            Version = Version,
            Signer = signer.Address,
            Properties = Properties,
        };
        return metadata.Sign(signer);
    }
}
