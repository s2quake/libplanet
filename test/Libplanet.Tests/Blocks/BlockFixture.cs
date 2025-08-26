using System.Security.Cryptography;
using Libplanet.Tests.Tx;
using Libplanet.Types;

namespace Libplanet.Tests.Blocks;

public class BlockFixture
{
    public const int ProtocolVersion = BlockHeader.CurrentProtocolVersion;

    public BlockFixture()
    {
        Miner = TestUtils.GenesisProposer;
        Genesis = TestUtils.ProposeGenesisBlock(
            protocolVersion: ProtocolVersion,
            proposer: Miner,
            previousStateRootHash: HashDigest<SHA256>.Parse(
                "e2e938f9d8af0a20d16d1c233fc4e8f39157145d003565807e4055ce6b5a0121"));
        TxFixture = new TxFixture(Genesis.BlockHash);
        Next = TestUtils.ProposeNextBlock(
            Genesis,
            proposer: Miner,
            protocolVersion: ProtocolVersion,
            previousStateRootHash: HashDigest<SHA256>.Parse(
                "6a648da9e91c21aa22bdae4e35c338406392aad0db4a0f998c01a7d7973cb8aa"),
            lastCommit: null,
            evidence: []);
        HasTx = TestUtils.ProposeNextBlock(
            Next,
            proposer: Miner,
            txs: [TxFixture.TxWithActions,],
            protocolVersion: ProtocolVersion,
            previousStateRootHash: HashDigest<SHA256>.Parse(
                "aaeda4f1a6a4aee7fc9a29014cff005109176e83a8e5d28876f2d889680e6421"),
            lastCommit: new BlockCommit
            {
                Height = Next.Height,
                Round = 0,
                BlockHash = Next.BlockHash,
                Votes =
                [
                    new VoteMetadata
                    {
                        Height = Next.Height,
                        Round = 0,
                        BlockHash = Next.BlockHash,
                        Timestamp = Next.Timestamp,
                        Validator = Miner.Address,
                        ValidatorPower = TestUtils.Validators.GetValidator(Miner.Address).Power,
                        Type = VoteType.PreCommit,
                    }.Sign(Miner),
                ],
            });
    }

    internal TxFixture TxFixture { get; }

    internal ISigner Miner { get; }

    internal Block Genesis { get; }

    internal Block Next { get; }

    internal Block HasTx { get; }
}
