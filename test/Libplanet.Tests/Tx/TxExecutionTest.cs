using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Tests.Tx
{
    public class TxExecutionTest(ITestOutputHelper output)
    {
        [Theory]
        [InlineData("")]
        [InlineData("SomeException")]
        public void Constructor(string exceptionName)
        {
            var random = RandomUtility.GetRandom(output);
            var blockHash = RandomUtility.BlockHash(random);
            var txId = RandomUtility.TxId(random);
            var inputState = RandomUtility.HashDigest<SHA256>(random);
            var outputState = RandomUtility.HashDigest<SHA256>(random);
            var exceptionNames = new List<string>() { exceptionName };
            var execution = new TxExecution
            {
                BlockHash = blockHash,
                TxId = txId,
                InputState = inputState,
                OutputState = outputState,
                ExceptionNames = [.. exceptionNames],
            };
            Assert.Equal(blockHash, execution.BlockHash);
            Assert.Equal(txId, execution.TxId);
            Assert.Equal(inputState, execution.InputState);
            Assert.Equal(outputState, execution.OutputState);
            Assert.Equal(exceptionNames, execution.ExceptionNames);
        }

        [Fact]
        public void EncodeDecode()
        {
            var random = RandomUtility.GetRandom(output);
            var execution = new TxExecution
            {
                BlockHash = RandomUtility.BlockHash(random),
                TxId = RandomUtility.TxId(random),
                InputState = RandomUtility.HashDigest<SHA256>(random),
                OutputState = RandomUtility.HashDigest<SHA256>(random),
                ExceptionNames = ["SomeException", "AnotherException"],
            };
            var encoded = ModelSerializer.SerializeToBytes(execution);
            var decoded = ModelSerializer.DeserializeFromBytes<TxExecution>(
                encoded);
            Assert.Equal(execution.BlockHash, decoded.BlockHash);
            Assert.Equal(execution.TxId, decoded.TxId);
            Assert.Equal(execution.Fail, decoded.Fail);
            Assert.Equal(execution.InputState, decoded.InputState);
            Assert.Equal(execution.OutputState, decoded.OutputState);
            Assert.Equal<string>(execution.ExceptionNames, decoded.ExceptionNames);
        }

        [Fact]
        public void ConstructorWithExceptions()
        {
            var random = RandomUtility.GetRandom(output);
            var blockHash = RandomUtility.BlockHash(random);
            var txId = RandomUtility.TxId(random);
            var inputState = RandomUtility.HashDigest<SHA256>(random);
            var outputState = RandomUtility.HashDigest<SHA256>(random);
            var exceptions = new List<Exception>() { new ArgumentException("Message") };
            var execution = new TxExecution
            {
                BlockHash = blockHash,
                TxId = txId,
                InputState = inputState,
                OutputState = outputState,
                ExceptionNames = [.. exceptions.Select(item => item.GetType().FullName)],
            };
            Assert.Equal(blockHash, execution.BlockHash);
            Assert.Equal(txId, execution.TxId);
            Assert.True(execution.Fail);
            Assert.Equal(inputState, execution.InputState);
            Assert.Equal(outputState, execution.OutputState);
            Assert.Equal(
                exceptions
                    .Select(exception => exception is Exception e
                        ? e.GetType().FullName
                        : null),
                execution.ExceptionNames);
        }
    }
}
