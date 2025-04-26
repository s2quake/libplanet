using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;

namespace Libplanet.Action.Tests.Common
{
    public sealed class UpdateValueAction : IAction
    {
        public UpdateValueAction()
        {
        }

        public UpdateValueAction(Address address, int increment)
        {
            Address = address;
            Increment = increment;
        }

        public Address Address { get; set; }

        public Integer Increment { get; set; }

        public IValue PlainValue => Bencodex.Types.Dictionary.Empty
            .Add("address", Address.ToBencodex())
            .Add("value", Increment);

        public void LoadPlainValue(IValue plainValue)
        {
            Address = Address.Create(((Dictionary)plainValue)["address"]);
            Increment = (Integer)((Dictionary)plainValue)["value"];
        }

        public IWorld Execute(IActionContext ctx)
        {
            IWorld states = ctx.World;
            IAccount account = states.GetAccount(ReservedAddresses.LegacyAccount);
            Integer value = account.GetState(Address) is Integer integer
                ? integer + Increment
                : Increment;

            account = account.SetState(Address, value);
            return states.SetAccount(ReservedAddresses.LegacyAccount, account);
        }
    }
}
