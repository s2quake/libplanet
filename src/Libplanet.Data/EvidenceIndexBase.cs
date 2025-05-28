using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public abstract class EvidenceIndexBase(IDatabase database, string name)
    : IndexBase<EvidenceId, EvidenceBase>(database.GetOrAdd(name))
{
    protected override byte[] GetBytes(EvidenceBase value) => ModelSerializer.SerializeToBytes(value);

    protected override EvidenceBase GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<EvidenceBase>(bytes);
}
