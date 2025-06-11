namespace Libplanet.Net
{
    /// <summary>
    /// A delegate to call back when a <see cref="Swarm"/> encounters
    /// a peer with a different <see cref="Protocol"/> signed by
    /// a trusted signer in the network.
    /// </summary>
    /// <param name="peer">The encountered <see cref="Peer"/> with
    /// a different <see cref="Protocol"/>.
    /// </param>
    /// <param name="peerVersion">The encountered different <see cref="Protocol"/>.
    /// </param>
    /// <param name="localVersion">The currently running application's
    /// <see cref="Protocol"/>.</param>
    public delegate void DifferentAppProtocolVersionEncountered(
        Peer peer,
        Protocol peerVersion,
        Protocol localVersion);
}
