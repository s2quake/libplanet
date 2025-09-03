using Libplanet.Types;

namespace Libplanet.Tests;

public static class TestUtils
{
    public const int Timeout = 30000;

    public static readonly ImmutableArray<ISigner> Signers =
    [
        PrivateKey.Parse("e5792a1518d9c7f7ecc35cd352899211a05164c9dde059c9811e0654860549ef").AsSigner(),
        PrivateKey.Parse("91d61834be824c952754510fcf545180eca38e036d3d9b66564f0667b30d5b93").AsSigner(),
        PrivateKey.Parse("b17c919b07320edfb3e6da2f1cfed75910322de2e49377d6d4d226505afca550").AsSigner(),
        PrivateKey.Parse("91602d7091c5c7837ac8e71a8d6b1ed1355cfe311914d9a76107899add0ad56a").AsSigner(),
    ];

    public static readonly ImmutableSortedSet<TestValidator> TestValidators =
    [
        .. Signers.Select(signer => new TestValidator(signer))
    ];

    // [0]: 0x1c54b2F83D26E2db2D93dE4539c301d8aE32E69d
    // [1]: 0x27A6F7321C93DE392d1078A7A3BdC62E03962cF7
    // [2]: 0x28D6C49FaAE09d58473D98c15DD1095C04232267
    // [3]: 0x66Bff9Ff1Ad108f26A829c1090bE517C0155801A
    public static readonly ImmutableSortedSet<Validator> Validators = [.. TestValidators.Select(v => (Validator)v)];

    public static readonly GenesisBlockBuilder GenesisBlockBuilder = new()
    {
        Validators = Validators,
    };

    public static BlockCommit CreateBlockCommit(
        Block block,
        bool deterministicTimestamp = false)
    {
        var useValidatorPower = true;
        return CreateBlockCommit(
            block.BlockHash, block.Height, 0, deterministicTimestamp, useValidatorPower);
    }

    public static BlockCommit CreateBlockCommit(
        BlockHash blockHash,
        int height,
        int round,
        bool deterministicTimestamp = false,
        bool useValidatorPower = true)
    {
        // Index #1 block cannot have lastCommit: There was no consensus of genesis block.
        if (height == 0)
        {
            throw new ArgumentException("Invalid block height", nameof(height));
        }

        // Using the unix epoch time as the timestamp of the vote if deterministicTimestamp is
        // flagged for getting a deterministic random value from RawHash.
        var votes = Signers.Select(key => new VoteMetadata
        {
            Height = height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = deterministicTimestamp ? DateTimeOffset.UnixEpoch : DateTimeOffset.UtcNow,
            Validator = key.Address,
            ValidatorPower = useValidatorPower ? Validators.GetValidator(key.Address).Power : 0,
            Type = VoteType.PreCommit,
        }.Sign(key)).ToImmutableArray();

        return new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = blockHash,
            Votes = votes,
        };
    }
}
