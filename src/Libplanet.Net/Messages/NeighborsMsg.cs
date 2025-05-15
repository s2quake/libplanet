using Destructurama.Attributed;
using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

public class NeighborsMsg : MessageContent
{
    public NeighborsMsg(IEnumerable<BoundPeer> found)
    {
        Found = found.ToImmutableList();
    }

    public NeighborsMsg(byte[][] dataFrames)
    {
        // var codec = new Codec();
        int foundCount = BitConverter.ToInt32(dataFrames[0], 0);
        Found = dataFrames.Skip(1).Take(foundCount)
            .Select(ba => ModelSerializer.DeserializeFromBytes<BoundPeer>(ba))
            .ToImmutableList();
    }

    [LogAsScalar]
    public IImmutableList<BoundPeer> Found { get; }

    public override MessageType Type => MessageType.Neighbors;

    public override IEnumerable<byte[]> DataFrames
    {
        get
        {
            var frames = new List<byte[]>();
            frames.Add(BitConverter.GetBytes(Found.Count));
            frames.AddRange(Found.Select(boundPeer => ModelSerializer.SerializeToBytes(boundPeer)));
            return frames;
        }
    }
}
