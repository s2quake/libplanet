using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Types.Assets;
using Libplanet.Types.Tx;
using Xunit;

namespace Libplanet.Tests.Tx
{
    public class TxExecutionTest
    {
        [Theory]
        [InlineData("")]
        [InlineData("SomeException")]
        public void Constructor( string exceptionName)
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
            var encoded = execution.ToBencodex();
            var decoded = TxExecution.Create(
                execution.BlockHash,
                execution.TxId,
                encoded);
            Assert.Equal(execution.BlockHash, decoded.BlockHash);
            Assert.Equal(execution.TxId, decoded.TxId);
            Assert.Equal(execution.Fail, decoded.Fail);
            Assert.Equal(execution.InputState, decoded.InputState);
            Assert.Equal(execution.OutputState, decoded.OutputState);
            Assert.Equal(execution.ExceptionNames, decoded.ExceptionNames);
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
                ExceptionNames = [.. exceptions.Select(item => item.GetType().Name)],
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

        [Fact]
        public void DecodeFailLegacy()
        {
            var random = new Random();
            var blockHash = random.NextBlockHash();
            var txId = random.NextTxId();

            Dictionary legacyEncoded = Dictionary.Empty
                .Add("fail", true)
                .Add("exc", "SomeException");
            var failExecution = TxExecution.Create(
                blockHash,
                txId,
                legacyEncoded);
            Assert.Equal(blockHash, failExecution.BlockHash);
            Assert.Equal(txId, failExecution.TxId);
            Assert.True(failExecution.Fail);
            Assert.Null(failExecution.InputState);
            Assert.Null(failExecution.OutputState);
            Assert.Null(failExecution.ExceptionNames);
        }

        [Fact]
        public void DecodeSuccessLegacy()
        {
            var random = new Random();
            var blockHash = random.NextBlockHash();
            var txId = random.NextTxId();

            // Note: Actual format for sDelta and updatedFAVs doesn't really matter,
            // it is important decoding doesn't throw an exception.
            var currency = new Currency("FOO", 0);
            Dictionary legacyEncoded = Dictionary.Empty
                .Add("fail", false)
                .Add("sDelta", Dictionary.Empty
                    .Add(random.NextAddress().ByteArray, random.NextAddress().ByteArray))
                .Add("updatedFAVs", List.Empty
                    .Add(currency.ToBencodex())
                    .Add(123));
            var successExecution = TxExecution.Create(
                blockHash,
                txId,
                legacyEncoded);
            Assert.Equal(blockHash, successExecution.BlockHash);
            Assert.Equal(txId, successExecution.TxId);
            Assert.False(successExecution.Fail);
            Assert.Null(successExecution.InputState);
            Assert.Null(successExecution.OutputState);
            Assert.Null(successExecution.ExceptionNames);
        }
    }
}
