using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using Destructurama.Attributed;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class Peer : IValidatableObject
{
    [NotDefault]
    public required Address Address { get; init; }

    public required DnsEndPoint EndPoint { get; init; }

    public IPAddress? PublicIPAddress { get; }

    public static Peer Parse(string s)
    {
        string[] tokens = s.Split(',');
        if (tokens.Length != 3)
        {
            throw new ArgumentException(
                $"'{s}', should have format <pubkey>,<host>,<port>",
                nameof(s));
        }

        if (!(tokens[0].Length == 130 || tokens[0].Length == 66))
        {
            throw new ArgumentException(
                $"'{s}', a length of public key must be 130 or 66 in hexadecimal," +
                $" but the length of given public key '{tokens[0]}' doesn't.",
                nameof(s));
        }

        try
        {
            var address = Address.Parse(tokens[0]);
            var host = tokens[1];
            var port = int.Parse(tokens[2], CultureInfo.InvariantCulture);

            return new Peer { Address = address, EndPoint = new DnsEndPoint(host, port) };
        }
        catch (Exception e)
        {
            throw new ArgumentException(
                $"{nameof(s)} seems invalid. [{s}]",
                nameof(s),
                innerException: e);
        }
    }

    public override string ToString()
    {
        return $"{Address},{EndPoint},{PublicIPAddress}";
    }

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
