using System;
using System.IO;
using Libplanet.Action;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Tests.Store;

namespace Libplanet.RocksDBStore.Tests
{
    public class RocksDBStoreFixture : StoreFixture
    {
        private readonly IStore _store;
        private readonly IStateStore _stateStore;

        public RocksDBStoreFixture(IAction? blockAction = null)
            : base(blockAction)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"rocksdb_test_{Guid.NewGuid()}"
            );

            Scheme = "rocksdb+file://";

            _store = new RocksDBStore(Path, blockCacheSize: 2, txCacheSize: 2);
            _stateStore = LoadTrieStateStore(Path);
        }

        public override IStore Store => _store;

        public override IStateStore StateStore => _stateStore;

        public override IKeyValueStore StateHashKeyValueStore =>
            throw new NotSupportedException();

        public override IKeyValueStore StateKeyValueStore =>
            throw new NotSupportedException();

        public IStateStore LoadTrieStateStore(string path)
        {
            IKeyValueStore stateKeyValueStore =
                new RocksDBKeyValueStore(System.IO.Path.Combine(path, "states"));
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
