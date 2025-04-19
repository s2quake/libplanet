using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Store.Trie;

/// <summary>
/// An interface for <see href="https://en.wikipedia.org/wiki/Merkle_tree">Merkle Tree</see>.
/// </summary>
/// <seealso cref="Trie"/>
public interface ITrie : IEnumerable<KeyValuePair<KeyBytes, IValue>>
{
    /// <summary>
    /// The root of the <see cref="ITrie"/>.  This is <see langword="null"/> if and only if
    /// the <see cref="ITrie"/> is empty.  That is, this is never a "hashed node" of a
    /// <see langword="null"/> root.
    /// </summary>
    /// <seealso cref="Hash"/>
    INode Node { get; }

    /// <summary>
    /// The state root hash of the trie.
    /// </summary>
    /// <remarks>
    /// If <see cref="Node"/> is <see langword="null"/>, this still gives a unique
    /// <see cref="HashDigest{SHA256}"/> value corresponding to <see langword="null"/>
    /// that is <em>never recorded</em>.
    /// </remarks>
    /// <seealso cref="Node"/>
    HashDigest<SHA256> Hash { get; }

    /// <summary>
    /// Whether <see cref="Node"/> is recorded in the store.
    /// </summary>
    /// <remarks>A <see cref="Node"/> that is <see langword="null"/> is always considered
    /// as recorded.
    /// </remarks>
    bool IsCommitted { get; }

    /// <summary>
    /// Gets or sets the value stored with the specified <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key used to store or retrieve a value.</param>
    /// <returns>The value associated with the specified <paramref name="key"/>.
    /// Absent value is represented as <see langword="null"/>.</returns>
    IValue this[in KeyBytes key] { get; }

    IValue this[in ImmutableArray<byte> key] => this[new KeyBytes(key)];

    /// <summary>
    /// Stores the <paramref name="value"/> at the path corresponding to
    /// given <paramref name="key"/> <em>in memory</em>.
    /// </summary>
    /// <param name="key">The unique key to associate with the <paramref name="value"/>.</param>
    /// <param name="value">The value to store.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the given
    /// <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <returns>Returns new updated <see cref="ITrie"/>.</returns>
    /// <remarks>
    /// This <em>should not</em> actually write anything to storage.
    /// Stored <paramref name="value"/> is actually written to storage when
    /// <see cref="IStateStore.Commit"/> is called.
    /// </remarks>
    /// <seealso cref="IStateStore.Commit"/>
    ITrie Set(in KeyBytes key, IValue value);

    ITrie Set(in ImmutableArray<byte> key, IValue value) => Set(new KeyBytes(key), value);

    ITrie Set(string key, IValue value) => Set((KeyBytes)key, value);

    /// <summary>
    /// Removes the value at the path corresponding to given <paramref name="key"/>
    /// <em>in memory</em>.  If there is no <see cref="IValue"/> at <paramref name="key"/>,
    /// this does nothing.
    /// </summary>
    /// <param name="key">The unique key to associate with the <paramref name="value"/>.</param>
    /// <returns>Returns new updated <see cref="ITrie"/>.</returns>
    /// <remarks>
    /// This <em>should not</em> actually remove anything from storage.
    /// The removal of the value at the marked path given by <paramref name="key"/> is actually
    /// recorded to storage when <see cref="IStateStore.Commit"/> is called.
    /// Regardless, there is actually no removal of any value from storage even when
    /// <see cref="IStateStore.Commit"/> is called.
    /// </remarks>
    /// <seealso cref="IStateStore.Commit"/>
    ITrie? Remove(in KeyBytes key);

    INode GetNode(in Nibbles key);

    INode GetNode(in KeyBytes key);

    bool TryGetNode(in KeyBytes key, [MaybeNullWhen(false)] out INode node);

    bool ContainsKey(in KeyBytes key);

    /// <summary>
    /// Lists every non-<see langword="null"/> <see cref="IValue"/> that is different
    /// from the one stored in <paramref name="other"/> given any <see cref="KeyBytes"/> path.
    /// </summary>
    /// <param name="other">The other <see cref="Trie"/> to compare to.</param>
    /// <returns>A list of tuples where each tuple consists of the path where
    /// the difference occurred, the "old" value from <paramref name="other"/> and
    /// the current "new" value.</returns>
    /// <exception cref="InvalidTrieNodeException">Thrown when the method fails
    /// to traverse the <see cref="ITrie"/>.</exception>
    /// <remarks>
    /// This operation has the following properties:
    /// <list type="bullet">
    ///     <item><description>
    ///         This operation is non-symmetric.  That is, in general,
    ///         <c>trieA.Diff(trieB)</c> and <c>trieB.Diff(trieA)</c> are not the same.
    ///     </description></item>
    ///     <item><description>
    ///         Values existing in <paramref name="other"/> but not in the source instance,
    ///         considered as <see langword="null"/> in the source, are not included in the
    ///         result.
    ///     </description></item>
    /// </list>
    /// </remarks>
    IEnumerable<(KeyBytes Path, IValue? TargetValue, IValue SourceValue)> Diff(ITrie other);
}
