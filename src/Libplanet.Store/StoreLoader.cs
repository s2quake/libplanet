namespace Libplanet.Store;

/// <summary>
/// A function that parses a URI and returns a pair of <see cref="Libplanet.Store.Store"/> and
/// <see cref="TrieStateStore"/>.
/// </summary>
/// <param name="storeUri">A URI referring to a store.</param>
/// <returns>A pair of loaded <see cref="Libplanet.Store.Store"/> and <see cref="TrieStateStore"/> instances.
/// </returns>
public delegate (Libplanet.Store.Store Store, TrieStateStore StateStore) StoreLoader(Uri storeUri);
