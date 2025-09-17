using System.ComponentModel;

namespace Libplanet.Commands.Keys;

public readonly record struct KeyInfo
{
    public KeyInfo()
    {
    }

    [DefaultValue("")]
    public string KeyId { get; init; } = string.Empty;

    [DefaultValue("")]
    public string PrivateKey { get; init; } = string.Empty;

    [DefaultValue("")]
    public string Address { get; init; } = string.Empty;

    [DefaultValue("")]
    public string PublicKey { get; init; } = string.Empty;
}
