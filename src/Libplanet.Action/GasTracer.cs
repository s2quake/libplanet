using System;
using System.Threading;
using Libplanet.Types.Tx;

namespace Libplanet.Action
{
    /// <summary>
    /// Provides a way to trace the gas usage of an <see cref="Transaction"/>.
    /// It will be initialize each transaction.
    ///
    /// <see cref="GasTracer"/> is thread-local, so it can be used in a multi-threaded environment.
    /// </summary>
    public static class GasTracer
    {
        private static readonly AsyncLocal<GasMeter> GasMeter = new AsyncLocal<GasMeter>();

        private static readonly AsyncLocal<bool> IsTrace = new AsyncLocal<bool>();

        private static readonly AsyncLocal<bool> IsTraceCancelled = new AsyncLocal<bool>();

        /// <summary>
        /// The amount of gas used so far.
        /// </summary>
        public static long GasUsed => GasMeterValue.GasUsed;

        /// <summary>
        /// The amount of gas available.
        /// </summary>
        public static long GasAvailable => GasMeterValue.GasAvailable;

        internal static bool IsTxAction { get; set; }

        private static GasMeter GasMeterValue
            => GasMeter.Value ?? throw new InvalidOperationException(
                "GasTracer is not initialized.");

        /// <summary>
        /// Using gas by the specified amount.
        /// </summary>
        /// <param name="gas">
        /// The amount of gas to use.
        /// </param>
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
}
