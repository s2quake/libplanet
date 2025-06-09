using Libplanet.Serialization;

namespace Libplanet.Types.Tests;

[Model(Version = 1, TypeName = "Libplanet_Types_Tests_TestEvidence")]
public sealed record class TestEvidence : EvidenceBase, IEquatable<TestEvidence>
{
}
