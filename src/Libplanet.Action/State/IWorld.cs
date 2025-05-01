// using Libplanet.Crypto;
// using Libplanet.Store.Trie;

// namespace Libplanet.Action.State;

// public interface World
// {
//     ITrie Trie { get; }

//     Address Signer { get; }

//     // int Version => Trie.GetMetadata() is { } value ? value.Version : 0;

//     ImmutableDictionary<Address, Account> Delta { get; }

//     Account GetAccount(Address address);

//     World SetAccount(Address address, Account account);
// }
