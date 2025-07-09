using System.ComponentModel.DataAnnotations;
using System.Net;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Net.Options;

public sealed record class TransportOptions
{
    [Required]
    public string Host { get; init; } = IPAddress.Loopback.ToString();

    [NonNegative]
    public int Port { get; init; } = 0;

    public Protocol Protocol { get; init; } = Protocol.Empty;

    public TimeSpan MessageLifetime { get; init; } = TimeSpan.MaxValue;
    
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(3);

    public TimeSpan ReceiveTimeout { get; init; } = TimeSpan.FromSeconds(3);
}
