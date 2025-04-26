// using System.Security.Cryptography;
// using Libplanet.Common;
// using Libplanet.Crypto;
// using Libplanet.Types.Tx;

// namespace Libplanet.Action;

// public interface CommittedActionContext
// {
//     Address Signer { get; }

//     TxId? TxId { get; }

//     Address Miner { get; }

//     long BlockHeight { get; }

//     int BlockProtocolVersion { get; }

//     HashDigest<SHA256> PreviousState { get; }

//     int RandomSeed { get; }

//     bool IsPolicyAction { get; }

//     IRandom GetRandom();
// }
