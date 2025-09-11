using System.Diagnostics.Contracts;
using System.Text.Json;
using Libplanet.Types;

namespace Libplanet.KeyStore.Kdfs;

/// <summary>
/// <a href="https://en.wikipedia.org/wiki/Scrypt">Scrypt</a>.
/// </summary>
public sealed class Scrypt : IKdf
{
    public Scrypt(
        int cost,
        byte[] salt,
        int keyLength,
        int parallelization,
        int blockSize)
        : this(
            cost,
            ImmutableArray.Create(salt, 0, salt.Length),
            keyLength,
            parallelization,
            blockSize)
    {
    }

    public Scrypt(
        int cost,
        in ImmutableArray<byte> salt,
        int keyLength,
        int parallelization,
        int blockSize)
    {
        if (cost < 2 || (cost & (cost - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cost),
                "Cost must be a power of 2 greater than 1!");
        }

        if (cost > int.MaxValue / 128 / blockSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cost),
                "Parameter cost is too large!");
        }

        if (blockSize > int.MaxValue / 128 / parallelization)
        {
            throw new ArgumentOutOfRangeException(
                nameof(blockSize),
                "Parameter blockSize is too large!");
        }

        Cost = cost;
        Salt = salt;
        KeyLength = keyLength;
        Parallelization = parallelization;
        BlockSize = blockSize;
    }

    public int Cost { get; }

    public int KeyLength { get; }

    public ImmutableArray<byte> Salt { get; }

    public int Parallelization { get; }

    public int BlockSize { get; }

    public string Name => "scrypt";

    public ImmutableArray<byte> Derive(string passphrase)
    {
        var key = Norgerman.Cryptography.Scrypt.ScryptUtil.Scrypt(
            passphrase, Salt.ToArray(), Cost, BlockSize, Parallelization, KeyLength);
        return ImmutableArray.Create(key, 0, KeyLength);
    }

    public string WriteJson(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("dklen", KeyLength);
        writer.WriteNumber("n", Cost);
        writer.WriteNumber("p", Parallelization);
        writer.WriteNumber("r", BlockSize);
        writer.WriteString("salt", ByteUtility.Hex(Salt));
        writer.WriteEndObject();
        return "scrypt";
    }

    public dynamic ToDynamic() => new
    {
        dklen = KeyLength,
        n = Cost,
        p = Parallelization,
        r = BlockSize,
        salt = ByteUtility.Hex(Salt)
    };

    public static Scrypt FromDynamic(dynamic @dynamic)
    {
        if (@dynamic.n is not int cost)
        {
            throw new ArgumentException("The \"n\" field must be an integer.", nameof(@dynamic));
        }

        if (@dynamic.r is not int blockSize)
        {
            throw new ArgumentException("The \"r\" field must be an integer.", nameof(@dynamic));
        }

        if (@dynamic.p is not int parallelization)
        {
            throw new ArgumentException("The \"p\" field must be an integer.", nameof(@dynamic));
        }

        if (@dynamic.dklen is not int keyLength)
        {
            throw new ArgumentException("The \"dklen\" field must be an integer.", nameof(@dynamic));
        }

        if (@dynamic.salt is not string saltString)
        {
            throw new ArgumentException("The \"salt\" field must be a string.", nameof(@dynamic));
        }

        var salt = ByteUtility.ParseHex(saltString);
        return new Scrypt(cost, salt, keyLength, parallelization, blockSize);
    }

    internal static IKdf FromJson(in JsonElement element)
    {
        if (!element.TryGetProperty("n", out JsonElement n))
        {
            throw new InvalidOperationException(
                "The \"kdfparams\" field must have a \"n\" field, ....");
        }

        if (n.ValueKind != JsonValueKind.Number || !n.TryGetInt32(out int cost))
        {
            throw new InvalidOperationException(
                "The \"n\" field, the number of iterations, must be a number.");
        }

        if (!element.TryGetProperty("r", out JsonElement r))
        {
            throw new InvalidOperationException(
                "The \"kdfparams\" field must have a \"r\" field, ....");
        }

        if (r.ValueKind != JsonValueKind.Number || !r.TryGetInt32(out int blockSize))
        {
            throw new InvalidOperationException(
                "The \"r\" field, the number of iterations, must be a number.");
        }

        if (!element.TryGetProperty("p", out JsonElement p))
        {
            throw new InvalidOperationException(
                "The \"kdfparams\" field must have a \"p\" field, ....");
        }

        if (p.ValueKind != JsonValueKind.Number || !p.TryGetInt32(out int parallelization))
        {
            throw new InvalidOperationException(
                "The \"n\" field, the number of iterations, must be a number.");
        }

        if (!element.TryGetProperty("dklen", out JsonElement dklen))
        {
            throw new InvalidOperationException(
                "The \"kdfparams\" field must have a \"dklen\" field, " +
                "the length of key in bytes.");
        }

        if (dklen.ValueKind != JsonValueKind.Number ||
            !dklen.TryGetInt32(out int keyLength))
        {
            throw new InvalidOperationException(
                "The \"dklen\" field, the length of key in bytes, must be a number.");
        }

        if (!element.TryGetProperty("salt", out JsonElement saltElement))
        {
            throw new InvalidOperationException(
                "The \"kdfparams\" field must have a \"salt\" field.");
        }

        string saltString;
        try
        {
            saltString = saltElement.GetString();
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("The \"salt\" field must be a string.");
        }

        byte[] salt;
        try
        {
            salt = ByteUtility.ParseHex(saltString);
        }
        catch (ArgumentNullException)
        {
            throw new InvalidOperationException(
                "The \"salt\" field must not be null, but a string.");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                "The \"salt\" field must be a hexadecimal string of bytes.\n" + e);
        }

        return new Scrypt(cost, salt, keyLength, parallelization, blockSize);
    }
}
