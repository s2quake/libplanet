using Xunit;

namespace Libplanet.Action.Tests.Sys
{
    public class RegistryTest
    {

        [Fact]
        public void Deserialize()
        {
            // Dictionary value = Dictionary.Empty
            //     .Add("type_id", 2)
            //     .Add(
            //         "values",
            //         new List(
            //             _validatorSet.Bencoded,
            //             Dictionary.Empty.Add(
            //                 default(Address).ToByteArray(),
            //                 "initial value")));
            // IAction action = Registry.Deserialize(value);
            // var initialize = Assert.IsType<Initialize>(action);
            // Assert.Equal(_validatorSet, initialize.ImmutableSortedSet<Validator>);
            // Assert.Equal(_states, initialize.States);

            // ArgumentException e;
            // e = Assert.Throws<ArgumentException>(
            //     () => Registry.Deserialize((Dictionary)value.Remove(new Text("type_id")))
            // );
            // Assert.Equal("serialized", e.ParamName);
            // Assert.Contains("type_id", e.Message);

            // e = Assert.Throws<ArgumentException>(
            //     () => Registry.Deserialize((Dictionary)value.Remove(new Text("values")))
            // );
            // Assert.Equal("serialized", e.ParamName);
            // Assert.Contains("values", e.Message);

            // e = Assert.Throws<ArgumentException>(
            //     () => Registry.Deserialize(value.SetItem("type_id", "non-integer"))
            // );
            // Assert.Equal("serialized", e.ParamName);
            // Assert.Contains("type_id", e.Message);

            // e = Assert.Throws<ArgumentException>(
            //     () => Registry.Deserialize(value.SetItem("type_id", short.MaxValue))
            // );
            // Assert.Contains(
            //     "Failed to deserialize",
            //     e.Message,
            //     StringComparison.InvariantCultureIgnoreCase);
        }

        [Fact]
        public void Serialize()
        {
            // var random = new System.Random();
            // Address addr = random.NextAddress();
            // IValue actual = new Initialize(_validatorSet, _states).PlainValue;
            // IValue expected = Dictionary.Empty
            //     .Add("type_id", 2)
            //     .Add(
            //         "values",
            //         new List(
            //             _validatorSet.Bencoded,
            //             Dictionary.Empty.Add(
            //                 default(Address).ToByteArray(),
            //                 "initial value")));
            // TestUtils.AssertBencodexEqual(expected, actual);
        }

        [Fact]
        public void IsSystemAction()
        {
            // var random = new System.Random();
            // Address addr = random.NextAddress();
            // Assert.True(Registry.IsSystemAction(new Initialize(_validatorSet, _states)));
            // Assert.False(Registry.IsSystemAction(DumbAction.Create((addr, "foo"))));

            // Assert.True(Registry.IsSystemAction(Dictionary.Empty
            //     .Add("type_id", new Integer(2))));
            // Assert.False(Registry.IsSystemAction(Dictionary.Empty
            //     .Add("type_id", new Integer(2308))));
            // Assert.False(Registry.IsSystemAction(Dictionary.Empty
            //     .Add("type_id", new Text("mint"))));
        }
    }
}
