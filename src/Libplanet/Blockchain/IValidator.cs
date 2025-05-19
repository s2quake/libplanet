namespace Libplanet.Blockchain;

public interface IValidator<in T>
{
    void Validate(T obj);
}
