using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Libplanet.Serialization.Descriptors;
using MessagePack;
// using Libplanet.Serialization.ModelConverters;

namespace Libplanet.Serialization;

public readonly ref struct ModelWriter
{
    private readonly ArrayBufferWriter<byte> _buffer;
    private readonly MessagePackWriter _writer;

    public ModelWriter()
    {
        _buffer = new ArrayBufferWriter<byte>();
        _writer = new MessagePackWriter(_buffer);
    }

    internal void Flush(MessagePackWriter writer)
    {
        _writer.Flush();
        writer.WriteRaw(_buffer.WrittenSpan);
    }

    public void Dispose()
    {
        // _writer.Flush();
    }

    // public void Write(string s)
    // {
    //     _writer.Write(s);
    // }
    
    public void Write(ReadOnlySpan<byte> bytes)
    {
        _writer.Write(bytes);
    }
}
