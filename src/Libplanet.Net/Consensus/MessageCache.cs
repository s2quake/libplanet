using System.Diagnostics.Contracts;
using Libplanet.Net.Messages;
using Libplanet.Serialization;

namespace Libplanet.Net.Consensus;

public class MessageCache
{
    private readonly object _lock = new();
    private readonly Dictionary<MessageId, MessageContent> _messages = [];

    public void Put(MessageContent message)
    {
        lock (_lock)
        {
            try
            {
                MessageId id = message.Id;
                _messages.Add(id, message);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException(
                    "A message with the same id already exists.",
                    nameof(message));
            }
        }
    }

    public MessageContent Get(MessageId id)
    {
        lock (_lock)
        {
            if (_messages.TryGetValue(id, out MessageContent? msg))
            {
                return NetMQMessageCodec.CreateMessage(msg.Type, ModelSerializer.SerializeToBytes(msg));
            }

            throw new KeyNotFoundException($"A message of id {id} does not exist.");
        }
    }

    public MessageId[] DiffFrom(IEnumerable<MessageId> ids)
    {
        lock (_lock)
        {
            return ids.Where(id => !_messages.TryGetValue(id, out _)).ToArray();
        }
    }

    public MessageId[] GetGossipIds()
    {
        lock (_lock)
        {
            return _messages.Keys.ToArray();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }
}
