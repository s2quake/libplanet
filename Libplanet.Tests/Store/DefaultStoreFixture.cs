using System;
using System.IO;
using Libplanet.Action;
using Libplanet.Store;
using Libplanet.Store.Trie;

namespace Libplanet.Tests.Store
{
    public class DefaultStoreFixture : StoreFixture, IDisposable
    {
        private readonly IStore _store;
        private readonly IStateStore _stateStore;

        public DefaultStoreFixture(bool memory = true, IAction? blockAction = null)
            : base(blockAction)
        {
            if (memory)
            {
                Path = null;
            }
            else
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"defaultstore_test_{Guid.NewGuid()}"
                );
            }

            Scheme = "default+file://";

            _store = new DefaultStore(Path, blockCacheSize: 2, txCacheSize: 2);
            _stateStore = LoadTrieStateStore(Path);
        }

        public override IStore Store => _store;

        public override IStateStore StateStore => _stateStore;

        public override IKeyValueStore StateHashKeyValueStore =>
            throw new NotSupportedException();

        public override IKeyValueStore StateKeyValueStore =>
            throw new NotSupportedException();

        public IStateStore LoadTrieStateStore(string? path)
        {
            IKeyValueStore stateKeyValueStore =
                new DefaultKeyValueStore(path is null
                    ? null
                    : System.IO.Path.Combine(path, "states"));
            return new TrieStateStore(stateKeyValueStore);
        }

        public override void Dispose()
        {
            Store?.Dispose();
            StateStore?.Dispose();

            if (!(Path is null))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
