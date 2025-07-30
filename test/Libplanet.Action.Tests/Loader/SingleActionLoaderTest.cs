// using Bencodex.Types;
// using Libplanet.Action.Loader;
// using Libplanet.Action.Tests.Common;
// using Libplanet.Types.Crypto;
// using Xunit;

// namespace Libplanet.Action.Tests.Loader
// {
//     public class SingleActionLoaderTest
//     {
//         [Fact]
//         public void Create()
//         {
//             var loader = new SingleActionLoader<Attack>();
//             Assert.Equal(typeof(Attack), loader.Type);
//             loader = new SingleActionLoader<BaseAction>();
//             Assert.Equal(typeof(BaseAction), loader.Type);
//         }

//         [Fact]
//         public void LoadAction()
//         {
//             var actionTypeLoader = new SingleActionLoader<Attack>();

//             var plainValue = Dictionary.Empty
//                 .Add("type_id", "attack")
//                 .Add("values", Dictionary.Empty
//                     .Add("weapon", "foo")
//                     .Add("target", "bar")
//                     .Add("target_address", new Binary(TestUtils.GetRandomBytes(Address.Size))));
//             var action = new Attack();
//             action.LoadPlainValue(plainValue);

//             var loadedAction = actionTypeLoader.LoadAction(action.PlainValue);
//             Assert.Equal(plainValue, loadedAction.PlainValue);
//             Assert.Throws<InvalidActionException>(
//                 () => actionTypeLoader.LoadAction(new Text("baz")));
//         }
//     }
// }
