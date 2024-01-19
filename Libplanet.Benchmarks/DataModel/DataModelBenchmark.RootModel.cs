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
        public class RootModel : StoreDataModel
        {
            private MidModel? _midModel;

            public RootModel()
                : base()
            {
                System.Random random = new System.Random(0);
                MidModel = new MidModel();
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

            public RootModel(Bencodex.Types.Dictionary encoded)
                : base(encoded)
            {
            }

            public MidModel MidModel
            {
                get => _midModel ?? throw new InvalidOperationException();
                private set => _midModel = value;
            }

            public ImmutableList<int> BigList { get; private set; } =
                ImmutableList.Create<int>();

            public ImmutableDictionary<Address, string> BigDict { get; private set; } =
                ImmutableDictionary.Create<Address, string>();
        }
    }
}
