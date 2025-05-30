using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public abstract class EvidenceIndexBase(IDatabase database, string name)
    : KeyedIndexBase<EvidenceId, EvidenceBase>(database.GetOrAdd(name))
{
    protected override byte[] ValueToBytes(EvidenceBase value) => ModelSerializer.SerializeToBytes(value);

    protected override EvidenceBase BytesToValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<EvidenceBase>(bytes);

    protected override string KeyToString(EvidenceId key) => key.ToString();

    protected override EvidenceId StringToKey(string key) => EvidenceId.Parse(key);
}
