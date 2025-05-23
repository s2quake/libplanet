namespace Libplanet.State;

internal class GasMeter
{
    public GasMeter(long gasLimit)
    {
        if (gasLimit < 0)
        {
            throw new InvalidOperationException();
        }

        GasLimit = gasLimit;
    }

    public long GasAvailable => GasLimit - GasUsed;

    public long GasLimit { get; private set; }

    public long GasUsed { get; private set; }

    public void UseGas(long gas)
    {
        if (gas < 0)
        {
            throw new InvalidOperationException();
        }

        var newGasUsed = checked(GasUsed + gas);

        if (newGasUsed > GasLimit)
        {
            GasUsed = GasLimit;
            throw new InvalidOperationException($"Gas limit exceeded: {GasLimit} < {newGasUsed}");
        }

        GasUsed = newGasUsed;
    }
}
