using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Crypto;

namespace Libplanet.Action.State;

public class AccountDiff
{
    private static readonly int _metadataKeyLength = 0;

    private static readonly int _addressKeyLength = Address.Size * 2;

    private static readonly int _currencyKeyLength = HashDigest<SHA1>.Size * 2;

    private static readonly int _stateKeyLength = _addressKeyLength;

    private static readonly int _fungibleAssetKeyLength =
        _addressKeyLength + _currencyKeyLength + 2;

    private static readonly int _totalSupplyKeyLength = _currencyKeyLength + 2;

    private static readonly int _validatorSetKeyLength = 3;

    private static readonly ImmutableDictionary<int, byte> _reverseConversionTable =
        new Dictionary<int, byte>()
        {
            [48] = 0,   // '0'
            [49] = 1,   // '1'
            [50] = 2,   // '2'
            [51] = 3,   // '3'
            [52] = 4,   // '4'
            [53] = 5,   // '5'
            [54] = 6,   // '6'
            [55] = 7,   // '7'
            [56] = 8,   // '8'
            [57] = 9,   // '9'
            [97] = 10,  // 'a'
            [98] = 11,  // 'b'
            [99] = 12,  // 'c'
            [100] = 13, // 'd'
            [101] = 14, // 'e'
            [102] = 15, // 'f'
        }.ToImmutableDictionary();

    private AccountDiff(ImmutableDictionary<Address, (object?, object)> stateDiff)
    {
        StateDiffs = stateDiff;
    }

    public ImmutableDictionary<Address, (object?, object)> StateDiffs { get; }

    public static AccountDiff Create(Account target, Account source)
        => Create(target.Trie, source.Trie);

    public static AccountDiff Create(ITrie target, ITrie source)
    {
        var rawDiffs = source.Diff(target).ToList();

        Dictionary<Address, (object?, object)> stateDiffs =
            new Dictionary<Address, (object?, object)>();

        foreach (var diff in rawDiffs)
        {
            // NOTE: Cannot use switch as some lengths cannot be derived as const.
            if (diff.Path.Length == _stateKeyLength)
            {
                var sd = ToStateDiff(diff);
                stateDiffs[sd.Address] = (sd.TargetValue, sd.SourceValue);
            }
            else if (diff.Path.Length == _fungibleAssetKeyLength)
            {
                continue;
            }
            else if (diff.Path.Length == _totalSupplyKeyLength)
            {
                continue;
            }
            else if (diff.Path.Length == _validatorSetKeyLength)
            {
                continue;
            }
            else if (diff.Path.Length == _metadataKeyLength)
            {
                continue;
            }
            else
            {
                throw new ArgumentException(
                    $"Encountered different values at an invalid location: {diff.Path}");
            }
        }

        return new AccountDiff(stateDiffs.ToImmutableDictionary());
    }

    internal static (Address Address, object? TargetValue, object SourceValue)
        ToStateDiff((KeyBytes Path, object? TargetValue, object SourceValue) encoded)
    {
        return (
            ToAddress(encoded.Path.ToByteArray()),
            encoded.TargetValue,
            encoded.SourceValue);
    }

    internal static Address FromStateKey(KeyBytes key)
    {
        if (key.Length != _stateKeyLength)
        {
            throw new ArgumentException(
                $"Given {nameof(key)} must be of length {_stateKeyLength}: {key.Length}");
        }

        byte[] buffer = new byte[Address.Size];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Pack(key.Bytes[i * 2], key.Bytes[(i * 2) + 1]);
        }

        return new Address([.. buffer]);
    }

    internal static Address ToAddress(byte[] bytes)
    {
        if (bytes.Length != _stateKeyLength)
        {
            throw new ArgumentException(
                $"Given {nameof(bytes)} must be of length {_stateKeyLength}: {bytes.Length}");
        }

        byte[] buffer = new byte[Address.Size];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Pack(bytes[i * 2], bytes[(i * 2) + 1]);
        }

        return new Address([.. buffer]);
    }

    // FIXME: Assumes both x and y are less than 16.
    private static byte Pack(byte x, byte y) =>
        (byte)((_reverseConversionTable[x] << 4) + _reverseConversionTable[y]);
}
