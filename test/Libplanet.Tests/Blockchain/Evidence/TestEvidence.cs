using Libplanet.Crypto;
using Libplanet.Types.Evidence;

namespace Libplanet.Tests.Blockchain.Evidence;

public sealed record class TestEvidence : EvidenceBase, IEquatable<TestEvidence>
{
    public static TestEvidence Create(
        long height, Address validatorAddress, DateTimeOffset timestamp)
    {
        return new TestEvidence
        {
            Height = height,
            TargetAddress = validatorAddress,
            Timestamp = timestamp,
        };
    }

    public Address ValidatorAddress => TargetAddress;

    protected override void OnVerify(IEvidenceContext evidenceContext)
    {
    }
}
