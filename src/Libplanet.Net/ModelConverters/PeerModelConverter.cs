using System.IO;
using System.Net;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.ModelConverters;

internal sealed class PeerModelConverter : ModelConverterBase<Peer>
{
    private static readonly IModelConverter _addressConverter=ModelResolver.GetConverter(typeof(Address));

    protected override Peer Deserialize(BinaryReader reader, ModelOptions options)
    {
        var address = (Address)_addressConverter.Deserialize(reader.BaseStream, options);
        var host = reader.ReadString();
        var port = reader.ReadInt32();
        return new Peer
        {
            Address = address,
            EndPoint = new DnsEndPoint(host, port),
        };
    }

    protected override void Serialize(Peer obj, BinaryWriter writer, ModelOptions options)
    {
        _addressConverter.Serialize(obj.Address, writer.BaseStream, options);
        writer.Write(obj.EndPoint.Host);
        writer.Write(obj.EndPoint.Port);
    }
}
