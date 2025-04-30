using System.Collections.Immutable;
using Libplanet.Serialization;

namespace Libplanet.Serialization.Tests;

public sealed class ModelUtilityTest
{
    [Fact]
    public void GetHashCode_Test()
    {
        var model1 = new Model
        {
            Version = 1,
            Values1 = [0, 1, 2],
            Values2 =
            [
                [0, 1, 2],
                [3, 4, 5],
                [6, 7, 8],
            ],
            ImmutableValues1 = [0, 1, 2],
            ImmutableValues2 =
            [
                [6, 7, 8],
                [3, 4, 5],
                [0, 1, 2],
            ],
        };
        var model2 = new Model
        {
            Version = 1,
            Values1 = [0, 1, 2],
            Values2 =
            [
                [0, 1, 2],
                [3, 4, 5],
                [6, 7, 8],
            ],
            ImmutableValues1 = [0, 1, 2],
            ImmutableValues2 =
            [
                [6, 7, 8],
                [3, 4, 5],
                [0, 1, 2],
            ],
        };

        var hash1 = ModelUtility.GetHashCode(model1);
        var hash2 = ModelUtility.GetHashCode(model2);

        Assert.Equal(hash1, hash2);
        Assert.True(model1.Equals(model2));
    }

    [Model(Version = 1)]
    public sealed record class Model : IEquatable<Model>
    {
        [Property(0)]
        public int Version { get; set; }

        [Property(1)]
        public int[] Values1 { get; set; } = [];

        [Property(2)]
        public int[][] Values2 { get; set; } = [];

        [Property(3)]
        public ImmutableArray<int> ImmutableValues1 { get; set; } = [];

        [Property(4)]
        public ImmutableArray<int[]> ImmutableValues2 { get; set; } = [];

        public bool Equals(Model? other) => ModelUtility.Equals(this, other);

        public override int GetHashCode() => ModelUtility.GetHashCode(this);
    }
}
