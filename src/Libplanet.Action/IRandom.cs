namespace Libplanet.Action;

public interface IRandom
{
    int Seed { get; }

    int Next();

    int Next(int upperBound);

    int Next(int lowerBound, int upperBound);

    void NextBytes(byte[] buffer);
}
