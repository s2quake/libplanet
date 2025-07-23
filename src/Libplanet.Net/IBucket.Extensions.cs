namespace Libplanet.Net;

public partial interface IBucket
{
    bool IsEmpty => Count == 0;

    bool IsFull => Count == Capacity;
}
