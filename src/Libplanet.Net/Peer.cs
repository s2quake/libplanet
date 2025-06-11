using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class Peer : IValidatableObject
{
    [NotDefault]
    public required Address Address { get; init; }

    // [RegularExpression(@"^(?:(?:25[0-5]|(?:2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$")]
    public required DnsEndPoint EndPoint { get; init; }

    public static Peer Parse(string s)
    {
        var tokens = s.Split(',');
        if (tokens.Length is not 3)
        {
            throw new FormatException($"'{s}', should have format <address>,<host>,<port>");
        }

        var address = Address.Parse(tokens[0]);
        var host = tokens[1];
        var port = int.Parse(tokens[2], CultureInfo.InvariantCulture);
        return new Peer
        {
            Address = address,
            EndPoint = new DnsEndPoint(host, port),
        };
    }

    public override string ToString() => $"{Address},{EndPoint.Host},{EndPoint.Port}";

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Uri.CheckHostName(EndPoint.Host) == UriHostNameType.Unknown)
        {
            yield return new ValidationResult(
                $"Given {nameof(EndPoint)} has unknown host name type: {EndPoint.Host}",
                [nameof(EndPoint)]);
        }
    }
}
