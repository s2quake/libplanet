namespace Libplanet.Net
{
    public class PeerNotFoundException : SwarmException
    {
        public PeerNotFoundException()
        {
        }

        public PeerNotFoundException(string message)
            : base(message)
        {
        }

        public PeerNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
