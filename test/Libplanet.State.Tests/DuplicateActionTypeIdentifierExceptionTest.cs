// using Libplanet.State.Tests.Common;
// using Xunit;

// namespace Libplanet.State.Tests
// {
//     public class DuplicateActionTypeIdentifierExceptionTest
//     {
//         private readonly InvalidOperationException _exception;

//         public DuplicateActionTypeIdentifierExceptionTest()
//         {
//             _exception = new InvalidOperationException(
//                 "An error message.",
//                 "type_id",
//                 ImmutableHashSet.Create(typeof(DumbAction), typeof(NullAction))
//             );
//         }

//         [Fact]
//         public void Props()
//         {
//             Assert.Equal("type_id", _exception.TypeIdentifier);
//             Assert.Equal(
//                 ImmutableHashSet.Create(typeof(DumbAction), typeof(NullAction)),
//                 _exception.DuplicateActionTypes
//             );
//         }

//         [Fact]
//         public void Message()
//         {
//             const string expected =
//                 "An error message.\n\nType ID: type_id\nAssociated types:\n" +
//                 "  Libplanet.State.NullAction\n  Libplanet.State.Tests.Common.DumbAction";
//             Assert.Equal(expected, _exception.Message);
//         }
//     }
// }
