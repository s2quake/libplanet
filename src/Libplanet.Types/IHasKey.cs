namespace Libplanet.Types;

public interface IHasKey<out T>
    where T : notnull
{
    T Key { get; }
}
