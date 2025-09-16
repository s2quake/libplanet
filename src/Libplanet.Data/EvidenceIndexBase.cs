using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public abstract class EvidenceIndexBase(IDatabase database, string name, int cacheSize = 100)
    : KeyedIndexBase<EvidenceId, EvidenceBase>(database.GetOrAdd(name), cacheSize)
{
    protected override byte[] ValueToBytes(EvidenceBase value) => ModelSerializer.Serialize(value);

    protected override EvidenceBase BytesToValue(byte[] bytes) => ModelSerializer.Deserialize<EvidenceBase>(bytes);

    protected override string KeyToString(EvidenceId key) => key.ToString();

    protected override EvidenceId StringToKey(string key) => EvidenceId.Parse(key);
}
