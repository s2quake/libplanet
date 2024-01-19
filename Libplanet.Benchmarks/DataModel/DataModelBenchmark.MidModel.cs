using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Libplanet.Crypto;
using StoreDataModel = Libplanet.Store.DataModel;

namespace Libplanet.Benchmarks.DataModel
{
    public partial class DataModelBenchmark
    {
        public class MidModel : StoreDataModel
        {
            private LeafModel? _leafModel;

            public MidModel()
                : base()
            {
                System.Random random = new System.Random(1);
                LeafModel = new LeafModel();
                BigList = Enumerable
                    .Range(0, 1000)
                    .Select(_ => random.Next())
                    .ToImmutableList();
                BigDict = Enumerable
                    .Range(0, 1000)
                    .Select(_ => new KeyValuePair<Address, string>(
                        new PrivateKey().Address,
                        new PrivateKey().Address.ToString()))
                    .ToImmutableDictionary();
            }

            public MidModel(Bencodex.Types.Dictionary encoded)
                : base(encoded)
            {
            }

            public LeafModel LeafModel
            {
                get => _leafModel ?? throw new InvalidOperationException();
                private set => _leafModel = value;
            }

            public ImmutableList<int> BigList { get; private set; } =
                ImmutableList.Create<int>();

            public ImmutableDictionary<Address, string> BigDict { get; private set; } =
                ImmutableDictionary.Create<Address, string>();
        }
    }
}
