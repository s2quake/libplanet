// using System.Runtime.CompilerServices;
// using Libplanet.Serialization;
// using Libplanet.Types.Assets;
// using Libplanet.Types.Blocks;
// using Libplanet.Types.Crypto;

// namespace Libplanet.Types.Tx;

// [Model(Version = 1)]
// public sealed record class UnsignedTx
// {
//     [Property(0)]
//     public required TxInvoice Invoice { get; init; }

//     [Property(1)]
//     public required TxSigningMetadata SigningMetadata { get; init; }

//     public ImmutableSortedSet<Address> UpdatedAddresses => Invoice.UpdatedAddresses;

//     public DateTimeOffset Timestamp => Invoice.Timestamp;

//     public BlockHash GenesisHash => Invoice.GenesisHash;

//     public ImmutableArray<ActionBytecode> Actions => Invoice.Actions;

//     public FungibleAssetValue? MaxGasPrice => Invoice.MaxGasPrice;

//     public long? GasLimit => Invoice.GasLimit;

//     public long Nonce => SigningMetadata.Nonce;

//     public Address Signer => SigningMetadata.Signer;

//     public static UnsignedTx Create(TxInvoice invoice, TxSigningMetadata signingMetadata) => new()
//     {
//         Invoice = invoice,
//         SigningMetadata = signingMetadata,
//     };

//     public ImmutableArray<byte> CreateSignature(PrivateKey privateKey)
//     {
//         if (!privateKey.Address.Equals(Signer))
//         {
//             throw new ArgumentException(
//                 "The given private key does not correspond to the public key.",
//                 paramName: nameof(privateKey));
//         }

//         var options = new ModelOptions
//         {
//             IsValidationEnabled = false,
//         };
//         var bytes = ModelSerializer.SerializeToBytes(this, options);
//         byte[] sig = privateKey.Sign(bytes);
//         ImmutableArray<byte> movedImmutableArray =
//             Unsafe.As<byte[], ImmutableArray<byte>>(ref sig);
//         return movedImmutableArray;
//     }

//     public bool VerifySignature(ImmutableArray<byte> signature)
//     {
//         var bytes = ModelSerializer.SerializeToBytes(this);
//         return PublicKey.Verify(Signer, [.. bytes], signature);
//     }

//     public Transaction Sign(PrivateKey privateKey) => new TransactionMetadata
// {this, privateKey);

//     public Transaction Verify(ImmutableArray<byte> signature)
//         => new() { UnsignedTx = this, Signature = signature };
// }
