using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.TestUtilities;

[Model(Version = 1, TypeName = "Libplanet_TestUtilities_TestEvidence")]
public sealed record class TestEvidence : EvidenceBase, IEquatable<TestEvidence>
{
}
