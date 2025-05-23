namespace Libplanet.State;

internal sealed class Random(int seed) : System.Random(seed), IRandom
{
    public int Seed { get; } = seed;
}
