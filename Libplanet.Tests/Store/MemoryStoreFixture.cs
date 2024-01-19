using System;
using Libplanet.Action;
using Libplanet.Store;
using Libplanet.Store.Trie;

namespace Libplanet.Tests.Store
{
    public class MemoryStoreFixture : StoreFixture
    {
        private readonly IStore _store;
        private readonly IStateStore _stateStore;

        public MemoryStoreFixture(IAction? blockAction = null)
            : base(blockAction)
        {
            _store = new MemoryStore();
            _stateStore = new TrieStateStore(new MemoryKeyValueStore());
        }

        public override IStore Store => _store;

        public override IStateStore StateStore => _stateStore;

        public override IKeyValueStore StateHashKeyValueStore =>
            throw new NotSupportedException();

        public override IKeyValueStore StateKeyValueStore =>
            throw new NotSupportedException();

        public override void Dispose()
        {
            Store?.Dispose();
            StateStore?.Dispose();
        }
    }
}
