using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed record class HaveMessage : MessageContent, IEquatable<HaveMessage
{
    // public HaveMessage(MessageId[] messageIds)
    // {
    //     Ids = messageIds;
    // }

    // public HaveMessage(byte[][] dataFrames)
    // {
    //     int msgCount = BitConverter.ToInt32(dataFrames[0], 0);
    //     Ids = dataFrames
    //         .Skip(1).Take(msgCount)
    //         .Select(ba => new MessageId(ba))
    //         .ToList();
    // }

    [Property(0)]
    public ImmutableArray<MessageId> Ids { get; }

    public override MessageType Type => MessageType.HaveMessage;

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
    
    public bool Equals(HaveMessage? other) => ModelResolver.Equals(this, other);

    // public override IEnumerable<byte[]> DataFrames
    // {
    //     get
    //     {
    //         var frames = new List<byte[]>();
    //         frames.Add(BitConverter.GetBytes(Ids.Count()));
    //         frames.AddRange(Ids.Select(id => id.ToByteArray()));
    //         return frames;
    //     }
    // }
}
