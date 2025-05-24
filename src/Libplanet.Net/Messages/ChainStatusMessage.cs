using Destructurama.Attributed;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class ChainStatusMessage : MessageContent
{
    // public ChainStatusMessage(
    //     int protocolVersion,
    //     BlockHash genesisHash,
    //     int tipIndex,
    //     BlockHash tipHash)
    // {
    //     ProtocolVersion = protocolVersion;
    //     GenesisHash = genesisHash;
    //     TipIndex = tipIndex;
    //     TipHash = tipHash;
    // }

    // public ChainStatusMessage(byte[][] dataFrames)
    // {
    //     ProtocolVersion = BitConverter.ToInt32(dataFrames[0], 0);
    //     GenesisHash = new BlockHash(dataFrames[1]);
    //     TipIndex = BitConverter.ToInt32(dataFrames[2], 0);
    //     TipHash = new BlockHash(dataFrames[3]);
    // }

    [Property(0)]
    public required int ProtocolVersion { get; init; }

    [LogAsScalar]
    [Property(1)]
    public required BlockHash GenesisHash { get; init; }

    [Property(2)]
    public required int TipIndex { get; init; }

    [LogAsScalar]
    public required BlockHash TipHash { get; init; }

    public override MessageType Type => MessageType.ChainStatus;

    // public override IEnumerable<byte[]> DataFrames => new[]
    // {
    //     BitConverter.GetBytes(ProtocolVersion),
    //     GenesisHash.Bytes.ToArray(),
    //     BitConverter.GetBytes(TipIndex),
    //     TipHash.Bytes.ToArray(),
    // };

    public static implicit operator BlockExcerpt(ChainStatusMessage msg) => new()
    {
        Height = msg.TipIndex,
        ProtocolVersion = msg.ProtocolVersion,
        BlockHash = msg.TipHash,
    };
}
