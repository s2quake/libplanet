using Libplanet.Serialization;
using Libplanet.Types.Evidence;

namespace Libplanet.Net.Messages;

internal sealed record class GetEvidenceMessage : MessageContent, IEquatable<GetEvidenceMessage>
{
    // public GetEvidenceMessage(ImmutableArray<EvidenceId> evidenceIds)
    // {
    //     EvidenceIds = evidenceIds;
    // }

    // public GetEvidenceMessage(IEnumerable<EvidenceId> evidenceIds)
    // {
    //     EvidenceIds = evidenceIds;
    // }

    // public GetEvidenceMessage(byte[][] dataFrames)
    // {
    //     int txCount = BitConverter.ToInt32(dataFrames[0], 0);
    //     EvidenceIds = dataFrames
    //         .Skip(1).Take(txCount)
    //         .Select(ba => new EvidenceId(ba))
    //         .ToList();
    // }

    public ImmutableArray<EvidenceId> EvidenceIds { get; }

    public override MessageType Type => MessageType.GetEvidence;

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
    
    public bool Equals(GetEvidenceMessage? other) => ModelResolver.Equals(this, other);

    // public override IEnumerable<byte[]> DataFrames
    // {
    //     get
    //     {
    //         var frames = new List<byte[]>();
    //         frames.Add(BitConverter.GetBytes(EvidenceIds.Count()));
    //         frames.AddRange(EvidenceIds.Select(id => id.Bytes.ToArray()));
    //         return frames;
    //     }
    // }
}
