using Libplanet;
using Libplanet.Net.Consensus;
using Libplanet.Types;

namespace Libplanet.TestUtilities.Extensions;

public static class SignExtensions
{
    public static Transaction Sign(this TransactionMetadata @this, PrivateKey privateKey)
        => @this.Sign(privateKey.AsSigner());

    public static Vote Sign(this VoteMetadata @this, PrivateKey privateKey)
        => @this.Sign(privateKey.AsSigner());

    public static Block Sign(this RawBlock @this, PrivateKey privateKey)
        => @this.Sign(privateKey.AsSigner());

    public static Proposal Sign(this ProposalMetadata @this, PrivateKey privateKey, Block block)
        => @this.Sign(privateKey.AsSigner(), block);

    public static VoteSetBits Sign(this VoteSetBitsMetadata @this, PrivateKey privateKey)
        => @this.Sign(privateKey.AsSigner());

    public static ProposalClaim Sign(this ProposalClaimMetadata @this, PrivateKey privateKey)
        => @this.Sign(privateKey.AsSigner());

    public static Maj23 Sign(this Maj23Metadata @this, PrivateKey privateKey)
        => @this.Sign(privateKey.AsSigner());

    public static Transaction Create(this TransactionBuilder @this, PrivateKey privateKey)
        => @this.Create(privateKey.AsSigner());

    public static Transaction Create(this TransactionBuilder @this, PrivateKey privateKey, Blockchain blockchain)
        => @this.Create(privateKey.AsSigner(), blockchain);

    public static Block Create(this BlockBuilder @this, PrivateKey privateKey)
        => @this.Create(privateKey.AsSigner());

    public static Block ProposeBlock(this Blockchain @this, PrivateKey privateKey)
        => @this.ProposeBlock(privateKey.AsSigner());

    public static Transaction Add(
        this StagedTransactionCollection @this, PrivateKey privateKey)
        => @this.Add(privateKey.AsSigner(), new());

    public static Transaction Add(
        this StagedTransactionCollection @this, PrivateKey privateKey, TransactionSubmission submission)
        => @this.Add(privateKey.AsSigner(), submission);
}
