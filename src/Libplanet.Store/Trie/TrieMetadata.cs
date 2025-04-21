using System;
using Bencodex;
using Bencodex.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Store.Trie;

public sealed record class TrieMetadata : IBencodable
{
    public TrieMetadata(int version)
    {
        if (version < BlockMetadata.WorldStateProtocolVersion ||
            version > BlockMetadata.CurrentProtocolVersion)
        {
            throw new ArgumentException(
                $"Given {nameof(version)} cannot be less than " +
                $"{BlockMetadata.WorldStateProtocolVersion} or greater than " +
                $"{BlockMetadata.CurrentProtocolVersion}: {version}",
                nameof(version));
        }

        Version = version;
    }

    public TrieMetadata(IValue value)
    {
        if (value is not List list)
        {
            var message = $"Given {nameof(value)} must be of type " +
                          $"{typeof(Binary)}: {value.GetType()}";
            throw new ArgumentException(message, nameof(value));
        }

        Version = (int)((Integer)list[0]).Value;
    }

    public int Version { get; }

    public IValue Bencoded => new List(new Integer(Version));
}
