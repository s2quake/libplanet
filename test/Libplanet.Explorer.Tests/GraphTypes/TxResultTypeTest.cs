using System.Security.Cryptography;
using GraphQL;
using GraphQL.Execution;
using Libplanet.Common;
using Libplanet.Explorer.GraphTypes;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;

namespace Libplanet.Explorer.Tests.GraphTypes
{
    public class TxResultTypeTest
    {
        [Theory]
        [MemberData(nameof(TestCases))]
        public async void Query(TxResult txResult, IDictionary<string, object> expected)
        {
            var query =
                @"{
                    txStatus
                    blockHeight
                    blockHash
                    inputState
                    outputState
                    exceptionNames
                }";

            var txResultType = new TxResultType();
            ExecutionResult result = await ExecuteQueryAsync(
                query,
                txResultType,
                source: txResult
            );
            Assert.Null(result.Errors);
            ExecutionNode executionNode = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
            IDictionary<string, object> dictionary
                = Assert.IsAssignableFrom<IDictionary<string, object>>(executionNode.ToValue());

            Assert.Equal(expected, dictionary);
        }

        public static IEnumerable<object[]> TestCases() {
            return new object[][] {
                new object[] {
                    new TxResult
                    {
                        TxStatus = TxStatus.SUCCESS,
                        BlockHeight = 0,
                        BlockHash = "45bcaa4c0b00f4f31eb61577e595ea58fb69c7df3ee612aa6eea945bbb0ce39d",
                        InputState = HashDigest<SHA256>.Parse(
                            "7146ddfb3594089795f6992a668a3ce7fde089aacdda68075e1bc37b14ebb06f"),
                        OutputState = HashDigest<SHA256>.Parse(
                            "72bb2e17da644cbca9045f5e689fae0323b6af56a0acab9fd828d2243b50df1c"),
                        ExceptionNames = [],
                    },
                    new Dictionary<string, object> {
                        ["txStatus"] = "SUCCESS",
                        ["blockHeight"] = 0L,
                        ["blockHash"]
                            = "45bcaa4c0b00f4f31eb61577e595ea58fb69c7df3ee612aa6eea945bbb0ce39d",
                        ["inputState"] =
                            "7146ddfb3594089795f6992a668a3ce7fde089aacdda68075e1bc37b14ebb06f",
                        ["outputState"] =
                            "72bb2e17da644cbca9045f5e689fae0323b6af56a0acab9fd828d2243b50df1c",
                        ["exceptionNames"] = new string[] { "" },
                    }
                },
                new object[] {
                    new TxResult
                    {
                        TxStatus = TxStatus.FAILURE,
                        BlockHeight = 0,
                        BlockHash = "45bcaa4c0b00f4f31eb61577e595ea58fb69c7df3ee612aa6eea945bbb0ce39d",
                        InputState = HashDigest<SHA256>.Parse(
                            "7146ddfb3594089795f6992a668a3ce7fde089aacdda68075e1bc37b14ebb06f"),
                        OutputState = HashDigest<SHA256>.Parse(
                            "7146ddfb3594089795f6992a668a3ce7fde089aacdda68075e1bc37b14ebb06f"),
                        ExceptionNames = ["SomeException"],
                    },
                    new Dictionary<string, object> {
                        ["txStatus"] = "FAILURE",
                        ["blockHeight"] = 0L,
                        ["inputState"] =
                            "7146ddfb3594089795f6992a668a3ce7fde089aacdda68075e1bc37b14ebb06f",
                        ["outputState"] =
                            "7146ddfb3594089795f6992a668a3ce7fde089aacdda68075e1bc37b14ebb06f",
                        ["blockHash"]
                            = "45bcaa4c0b00f4f31eb61577e595ea58fb69c7df3ee612aa6eea945bbb0ce39d",
                        ["exceptionNames"] = new string[] { "SomeException" },
                    }
                },
                new object[] {
                    new TxResult
                    {
                        TxStatus = TxStatus.INCLUDED,
                        BlockHeight = 0,
                        BlockHash = "45bcaa4c0b00f4f31eb61577e595ea58fb69c7df3ee612aa6eea945bbb0ce39d",
                        ExceptionNames = [],
                    },
                    new Dictionary<string, object> {
                        ["txStatus"] = "INCLUDED",
                        ["blockHeight"] = 0L,
                        ["blockHash"]
                            = "45bcaa4c0b00f4f31eb61577e595ea58fb69c7df3ee612aa6eea945bbb0ce39d",
                        ["inputState"] = null,
                        ["outputState"] = null,
                        ["exceptionNames"] = new string[] { "" },
                    }
                },
                new object[] {
                    new TxResult
                    {
                        TxStatus = TxStatus.INVALID,
                        ExceptionNames = [],
                    },
                    new Dictionary<string, object> {
                        ["txStatus"] = "INVALID",
                        ["blockHeight"] = null,
                        ["blockHash"] = null,
                        ["inputState"] = null,
                        ["outputState"] = null,
                        ["exceptionNames"] = new string[] { "" },
                    }
                },
                new object[] {
                    new TxResult
                    {
                        TxStatus = TxStatus.STAGING,
                        ExceptionNames = [],
                    },
                    new Dictionary<string, object> {
                        ["txStatus"] = "STAGING",
                        ["blockHeight"] = null,
                        ["blockHash"] = null,
                        ["inputState"] = null,
                        ["outputState"] = null,
                        ["exceptionNames"] = new string[] { "" },
                    }
                }
            };
        }
    }
}
