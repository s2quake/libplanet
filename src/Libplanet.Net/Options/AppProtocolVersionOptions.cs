using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Options
{
    /// <summary>
    /// Various options for a <see cref="Swarm"/>'s
    /// <see cref="Net.Protocol"/>.
    /// </summary>
    public class AppProtocolVersionOptions
    {
        /// <summary>
        /// The application protocol version to comply.
        /// </summary>
        public Protocol AppProtocolVersion { get; set; } = default;

        /// <summary>
        /// The set of <see cref="PublicKey"/>s to trust when a node encounters
        /// a <see cref="Message"/> with an <see cref="Net.Protocol"/> that is different
        /// from <see cref="AppProtocolVersion"/>.  Any <see cref="Message"/> with an untrusted
        /// <see cref="Net.Protocol"/> is ignored by the node.  Set to an
        /// empty set of <see cref="PublicKey"/>s by default, i.e. not to trust any
        /// <see cref="Message"/> with a different <see cref="Net.Protocol"/>.
        /// </summary>
        public IImmutableSet<PublicKey> TrustedAppProtocolVersionSigners { get; set; } =
            ImmutableHashSet<PublicKey>.Empty;

        /// <summary>
        /// The callback triggered when a node encounters
        /// an <see cref="Net.Protocol"/> that is different from
        /// <see cref="AppProtocolVersion"/> that is signed by
        /// a <em>trusted party</em>, that is, one of
        /// <see cref="TrustedAppProtocolVersionSigners"/>.  Does nothing by default.
        /// </summary>
        public DifferentAppProtocolVersionEncountered
            DifferentAppProtocolVersionEncountered { get; set; } =
                (peer, peerVersion, localVersion) => { };
    }
}
