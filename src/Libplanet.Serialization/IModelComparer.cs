namespace Libplanet.Serialization;

internal interface IModelComparer
{
    bool Equals(object obj1, object obj2, Type type);

    int GetHashCode(object obj, Type type);
}
