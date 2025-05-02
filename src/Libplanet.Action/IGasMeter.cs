namespace Libplanet.Action;

public interface IGasMeter
{
    long GasAvailable { get; }

    long GasLimit { get; }

    long GasUsed { get; }
}
