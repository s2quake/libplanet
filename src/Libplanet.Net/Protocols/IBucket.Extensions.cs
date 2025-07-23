namespace Libplanet.Net.Protocols;

internal partial interface IBucket
{
    bool IsEmpty => Count == 0;

    bool IsFull => Count == Capacity;
}
