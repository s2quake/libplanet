using System.Buffers;
using MessagePack;

namespace Libplanet.Serialization;

public ref struct ModelReader
{
    internal ModelReader(scoped in ReadOnlySequence<byte> reader)
    {
        _reader = new MessagePackReader(reader);
    }

    private readonly MessagePackReader _reader = new();

    public byte[] ReadBytes()
    {
        if (_reader.ReadBytes() is { } bytes)
        {
            return bytes.ToArray();
        }
        else
        {
            throw new InvalidOperationException("No bytes to read.");
        }
    }
}
