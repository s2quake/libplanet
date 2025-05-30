using Libplanet.Serialization;

namespace Libplanet.Types.Tests;

[Model(Version = 1)]
public sealed record class TestEvidence : EvidenceBase, IEquatable<TestEvidence>
{
}
