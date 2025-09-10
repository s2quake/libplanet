namespace Libplanet.KeyStore;

public interface IKeyStore
{
    IEnumerable<Guid> ListIds();

    IEnumerable<Tuple<Guid, ProtectedPrivateKey>> List();

    ProtectedPrivateKey Get(Guid id);

    Guid Add(ProtectedPrivateKey key);

    void Remove(Guid id);
}
