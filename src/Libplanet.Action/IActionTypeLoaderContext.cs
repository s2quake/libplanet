namespace Libplanet.Action
{
    /// <summary>
    /// An interface to provide contextual variables to load action types.
    /// </summary>
    public interface IActionTypeLoaderContext
    {
        /// <summary>
        /// A block height.
        /// </summary>
        long Index { get; }
    }
}
