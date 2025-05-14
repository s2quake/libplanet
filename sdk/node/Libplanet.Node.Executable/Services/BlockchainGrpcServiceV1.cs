using Grpc.Core;
using Libplanet.Node.Services;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Node.API.Services;

public class BlockchainGrpcServiceV1(IReadChainService blockChain) : Blockchain.BlockchainBase
{
    private readonly IReadChainService _blockChain = blockChain;

    public override Task<GetGenesisBlockReply> GetGenesisBlock(
        GetGenesisBlockRequest request,
        ServerCallContext context)
    {
        return Task.FromResult(new GetGenesisBlockReply
        {
            Hash = _blockChain.Tip.BlockHash.ToString(),
        });
    }

    public override Task<GetTipReply> GetTip(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new GetTipReply
        {
            Hash = _blockChain.Tip.BlockHash.ToString(),
            Height = _blockChain.Tip.Height,
        });
    }

    public override Task<GetBlockReply> GetBlock(GetBlockRequest request, ServerCallContext context)
    {
        return Task.Run(GetResult);

        Block GetBlock() => request.BlockIdentifierCase switch
        {
            GetBlockRequest.BlockIdentifierOneofCase.Hash
                => _blockChain.GetBlock(BlockHash.Parse(request.Hash)),
            GetBlockRequest.BlockIdentifierOneofCase.Height
                => _blockChain.GetBlock(request.Height),
            _ => throw new InvalidOperationException("Invalid block identifier."),
        };

        GetBlockReply GetResult()
        {
            var block = GetBlock();
            return new GetBlockReply
            {
                Hash = block.BlockHash.ToString(),
                Height = block.Height,
                Proposer = block.Proposer.ToString(),
                PreviousHash = $"{block.PreviousHash}",
                StateRootHash = $"{block.StateRootHash}",
                Signature = $"{ByteUtility.Hex(block.Signature)}",
                ProtocolVersion = block.Version,
            };
        }
    }
}
