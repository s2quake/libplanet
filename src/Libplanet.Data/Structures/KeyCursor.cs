namespace Libplanet.Data.Structures;

internal readonly record struct KeyCursor : IEquatable<KeyCursor>
{
    internal KeyCursor(string key) => Key = key;

    public string Key { get; }

    public int Length => Key.Length;

    public bool IsEnd => Position == Length;

    public int Position { get; init; }

    public char Current => Key[Position];

    public KeyCursor NextCursor => this[Position..];

    public char this[Index index] => Key[index];

    public KeyCursor this[Range range]
    {
        get
        {
            var (position, length) = range.GetOffsetAndLength(Key.Length);
            if (position < 0 || position > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            if (length < 0 || position + length > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            return new KeyCursor(Key[range]);
        }
    }

    public KeyCursor Next(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        return new KeyCursor(Key) { Position = Position + offset };
    }

    public KeyCursor Next(int position, in KeyCursor cursor)
    {
        var minLength = Math.Min(Length - position, cursor.Length);
        var count = 0;

        for (var i = 0; i < minLength; i++)
        {
            if (Key[i + position] != cursor[i])
            {
                break;
            }

            count++;
        }

        return Next(count);
    }

    public bool StartsWith(string key)
    {
        if (Length < key.Length)
        {
            return false;
        }

        for (var i = 0; i < key.Length; i++)
        {
            if (Key[i] != key[i])
            {
                return false;
            }
        }

        return true;
    }

    public bool Equals(KeyCursor other) => Position == other.Position && Key.SequenceEqual(other.Key);

    public override int GetHashCode()
    {
        var code = 0;
        unchecked
        {
            var chars = Key;
            foreach (char @char in chars)
            {
                code = (code * 397) ^ @char.GetHashCode();
            }
        }

        return code ^ Position;
    }

    public override string ToString()
    {
        var s = Key;
        if (Position < Length)
        {
            s = s.Insert(Position + 1, "\u0332");
        }
        else if (Position == Length)
        {
            s += "\u0332";
        }

        return s;
    }
}
