using Libplanet.Serialization;
using Libplanet.Types.Evidence;

namespace Libplanet.Data;

public abstract class EvidenceStoreBase(IDatabase database, string name)
    : StoreBase<EvidenceId, EvidenceBase>(database.GetOrAdd(name))
{
    protected override byte[] GetBytes(EvidenceBase value) => ModelSerializer.SerializeToBytes(value);

    protected override EvidenceBase GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<EvidenceBase>(bytes);
}
