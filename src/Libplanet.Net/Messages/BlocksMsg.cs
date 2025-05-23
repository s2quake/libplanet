namespace Libplanet.Net.Messages;

internal sealed record class BlocksMessage : MessageContent
{
    // public BlocksMessage(IEnumerable<byte[]> payloads)
    // {
    //     var count = payloads.Count();
    //     if (count % 2 != 0)
    //     {
    //         throw new ArgumentException(
    //             $"Given {nameof(payloads)} must be of even length: {count}");
    //     }

    //     Payloads = payloads.ToList();
    // }

    // public BlocksMessage(byte[][] dataFrames)
    // {
    //     var count = BitConverter.ToInt32(dataFrames.First(), 0);
    //     if (count % 2 != 0)
    //     {
    //         throw new ArgumentException(
    //             $"Given {nameof(dataFrames)} must be of even length: {count}");
    //     }

    //     Payloads = dataFrames.Skip(1).Take(count).ToList();
    // }

    public ImmutableArray<byte[]> Payloads { get; init; }

    public override MessageType Type => MessageType.Blocks;

    // public override IEnumerable<byte[]> DataFrames
    // {
    //     get
    //     {
    //         var frames = new List<byte[]>();
    //         frames.Add(BitConverter.GetBytes(Payloads.Count));
    //         frames.AddRange(Payloads);
    //         return frames;
    //     }
    // }
}
