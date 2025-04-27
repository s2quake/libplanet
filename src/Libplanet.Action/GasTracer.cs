using System.Threading;

namespace Libplanet.Action;

public static class GasTracer
{
    private static readonly AsyncLocal<GasMeter> GasMeter = new AsyncLocal<GasMeter>();

    private static readonly AsyncLocal<bool> IsTrace = new AsyncLocal<bool>();

    private static readonly AsyncLocal<bool> IsTraceCancelled = new AsyncLocal<bool>();

    public static long GasUsed => GasMeterValue.GasUsed;

    public static long GasAvailable => GasMeterValue.GasAvailable;

    internal static bool IsTxAction { get; set; }

    private static GasMeter GasMeterValue
        => GasMeter.Value ?? throw new InvalidOperationException(
            "GasTracer is not initialized.");

    public static void UseGas(long gas)
    {
        if (IsTrace.Value)
        {
            GasMeterValue.UseGas(gas);
            if (IsTraceCancelled.Value)
            {
                throw new InvalidOperationException("GasTracing was canceled.");
            }
        }
    }

    public static void CancelTrace()
    {
        if (!IsTxAction)
        {
            throw new InvalidOperationException("CancelTrace can only be called in TxAction.");
        }

        if (IsTraceCancelled.Value)
        {
            throw new InvalidOperationException("GasTracing is already canceled.");
        }

        IsTraceCancelled.Value = true;
    }

    internal static void Initialize(long gasLimit)
    {
        GasMeter.Value = new GasMeter(gasLimit);
        IsTrace.Value = true;
        IsTraceCancelled.Value = false;
    }

    internal static void Release()
    {
        IsTrace.Value = false;
        IsTraceCancelled.Value = false;
    }
}
