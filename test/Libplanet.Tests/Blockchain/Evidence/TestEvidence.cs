using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Tests.Blockchain.Evidence;

[Model(Version = 1)]
public sealed record class TestEvidence : EvidenceBase, IEquatable<TestEvidence>
{
    public Address ValidatorAddress => TargetAddress;

    public static TestEvidence Create(int height, Address validatorAddress, DateTimeOffset timestamp) => new()
    {
        Height = height,
        TargetAddress = validatorAddress,
        Timestamp = timestamp,
    };
}
