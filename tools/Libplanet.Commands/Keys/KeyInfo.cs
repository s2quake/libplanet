using Libplanet.Serialization;

namespace Libplanet.Commands.Keys;

[Model("Keys_KeyInfo", Version = 1)]
public readonly record struct KeyInfo
{
    public KeyInfo()
    {
    }

    [Property(0)]
    public string KeyId { get; init; } = string.Empty;

    [Property(1)]
    public string PrivateKey { get; init; } = string.Empty;

    [Property(2)]
    public string Address { get; init; } = string.Empty;

    [Property(3)]
    public string PublicKey { get; init; } = string.Empty;
}
