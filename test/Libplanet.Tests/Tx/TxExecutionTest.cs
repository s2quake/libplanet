using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Tx;
using Xunit;

namespace Libplanet.Tests.Tx
{
    public class TxExecutionTest
    {
        [Theory]
        [InlineData("")]
        [InlineData("SomeException")]
        public void Constructor(string exceptionName)
        {
            var random = new Random();
            var blockHash = random.NextBlockHash();
            var txId = random.NextTxId();
            var inputState = random.NextHashDigest<SHA256>();
            var outputState = random.NextHashDigest<SHA256>();
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
            var random = new Random();
            var execution = new TxExecution
            {
                BlockHash = random.NextBlockHash(),
                TxId = random.NextTxId(),
                InputState = random.NextHashDigest<SHA256>(),
                OutputState = random.NextHashDigest<SHA256>(),
                ExceptionNames = ["SomeException", "AnotherException"],
            };
            var encoded = ModelSerializer.Serialize(execution);
            var decoded = ModelSerializer.Deserialize<TxExecution>(
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
            var random = new Random();
            var blockHash = random.NextBlockHash();
            var txId = random.NextTxId();
            var inputState = random.NextHashDigest<SHA256>();
            var outputState = random.NextHashDigest<SHA256>();
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
