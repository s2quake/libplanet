using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;

namespace Libplanet.Action.Tests.Common
{
    /// <summary>
    /// An <see cref="IAction"/> for testing.  In addition to simply setting
    /// an <see cref="IValue"/> to a certain <see cref="Crypto.Address"/>,
    /// records various data that can be accessed from <see cref="IActionContext"/>.
    /// </summary>
    public class ContextRecordingAction : IAction
    {
        /// <summary>
        /// The <see cref="Crypto.Address"/> where <see cref="IActionContext.Miner"/>
        /// will be recorded.
        /// </summary>
        public static readonly Address MinerRecordAddress =
            Address.Parse("1000000000000000000000000000000000000001");

        /// <summary>
        /// The <see cref="Crypto.Address"/> where <see cref="IActionContext.Signer"/>
        /// will be recorded.
        /// </summary>
        public static readonly Address SignerRecordAddress =
            Address.Parse("1000000000000000000000000000000000000002");

        /// <summary>
        /// The <see cref="Crypto.Address"/> where <see cref="IActionContext.BlockHeight"/>
        /// will be recorded.
        /// </summary>
        public static readonly Address BlockIndexRecordAddress =
            Address.Parse("1000000000000000000000000000000000000003");

        /// <summary>
        /// The <see cref="Crypto.Address"/> where the next random integer from
        /// <see cref="IActionContext.GetRandom()"/> will be recorded.
        /// </summary>
        public static readonly Address RandomRecordAddress =
            Address.Parse("1000000000000000000000000000000000000004");

        public ContextRecordingAction()
        {
        }

        public ContextRecordingAction(Address address, IValue value)
        {
            Address = address;
            Value = value;
        }

        public IValue PlainValue => Dictionary.Empty
            .Add("address", Address.ToBencodex())
            .Add("value", Value);

        private Address Address { get; set; }

        private IValue Value { get; set; }

        public void LoadPlainValue(IValue plainValue)
        {
            Address = Address.Create(((Dictionary)plainValue)["address"]);
            Value = ((Dictionary)plainValue)["value"];
        }

        public IWorld Execute(IActionContext context)
        {
            IWorld states = context.PreviousState;
            IAccount account = states.GetAccount(ReservedAddresses.LegacyAccount);
            account = account
                .SetState(Address, Value)
                .SetState(MinerRecordAddress, new Binary(context.Miner.Bytes))
                .SetState(SignerRecordAddress, new Binary(context.Signer.Bytes))
                .SetState(BlockIndexRecordAddress, new Integer(context.BlockHeight))
                .SetState(RandomRecordAddress, new Integer(context.GetRandom().Next()));
            return states.SetAccount(ReservedAddresses.LegacyAccount, account);
        }
    }
}
