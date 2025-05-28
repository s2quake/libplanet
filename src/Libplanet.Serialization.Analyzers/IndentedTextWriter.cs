using System.Text;

namespace Libplanet.Serialization.Analyzers;

public sealed class IndentedTextWriter : TextWriter
{
    private readonly StringBuilder _sb = new();

    public override Encoding Encoding => Encoding.UTF8;

    public int Depth { get; private set; }

    public int Position { get; private set; }

    public int Indent()
    {
        if (Position is not 0)
        {
            throw new InvalidOperationException("Cannot indent when not at the start of a line.");
        }

        Depth++;
        return Depth;
    }

    public int Unindent()
    {
        if (Position is not 0)
        {
            throw new InvalidOperationException("Cannot indent when not at the start of a line.");
        }

        if (Depth <= 0)
        {
            throw new InvalidOperationException("Cannot unindent below zero depth.");
        }

        Depth--;
        return Depth;
    }

    public override void Write(char value)
    {
        if (value == '\n')
        {
            _sb.Append(value);
            Position = 0;
        }
        else if (value == '\r')
        {
            // Ignore carriage return
        }
        else
        {
            if (Position == 0 && Depth > 0)
            {
                _sb.Append(new string(' ', Depth * 4));
            }

            _sb.Append(value);
            Position++;
        }
    }

    public override void Write(string? value)
    {
        if (value is null)
        {
            return;
        }

        foreach (char c in value)
        {
            Write(c);
        }
    }

    public override void WriteLine(string? value)
    {
        if (value is null)
        {
            Write('\n');
        }
        else
        {
            Write(value);
            Write('\n');
        }

        Position = 0;
    }

    public override string ToString() => _sb.ToString();
}
