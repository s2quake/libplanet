using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed record class WantMessage : MessageContent
{
    // public WantMessage(MessageId[] messageIds)
    // {
    //     Ids = messageIds;
    // }

    // public WantMessage(byte[][] dataFrames)
    // {
    //     int msgCount = BitConverter.ToInt32(dataFrames[0], 0);
    //     Ids = dataFrames
    //         .Skip(1).Take(msgCount)
    //         .Select(ba => new MessageId(ba))
    //         .ToList();
    // }

    [Property(0)]
    public ImmutableArray<MessageId> Ids { get; init; } = [];

    public override MessageType Type => MessageType.WantMessage;

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
