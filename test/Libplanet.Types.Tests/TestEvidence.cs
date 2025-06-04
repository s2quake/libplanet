using Libplanet.Serialization;

namespace Libplanet.Types.Tests;

[Model(Version = 1, TypeName = "Libplanet.Types.Tests.TestEvidence")]
public sealed record class TestEvidence : EvidenceBase, IEquatable<TestEvidence>
{
}
