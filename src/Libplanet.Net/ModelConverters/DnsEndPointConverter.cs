using System.Globalization;
using System.IO;
using System.Net;
using Libplanet.Serialization;

namespace Libplanet.Net.ModelConverters;

internal sealed class DnsEndPointConverter : ModelConverterBase<DnsEndPoint>
{
    protected override DnsEndPoint Deserialize(BinaryReader reader, ModelOptions options)
    {
        var text = reader.ReadString();
        var items = text.Split(':');
        return new DnsEndPoint(items[0], int.Parse(items[1], CultureInfo.InvariantCulture));
    }

    protected override void Serialize(DnsEndPoint obj, BinaryWriter writer, ModelOptions options)
    {
        writer.Write($"{obj.Host}:{obj.Port}");
    }
}
