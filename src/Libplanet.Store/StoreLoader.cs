namespace Libplanet.Store;

/// <summary>
/// A function that parses a URI and returns a pair of <see cref="IStore"/> and
/// <see cref="TrieStateStore"/>.
/// </summary>
/// <param name="storeUri">A URI referring to a store.</param>
/// <returns>A pair of loaded <see cref="IStore"/> and <see cref="TrieStateStore"/> instances.
/// </returns>
public delegate (IStore Store, TrieStateStore StateStore) StoreLoader(Uri storeUri);
