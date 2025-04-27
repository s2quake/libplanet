namespace Libplanet.Action;

public static class RandomExtensions
{
    public static Guid NextGuid(this IRandom random)
    {
        var b = new byte[16];
        random.NextBytes(b);

        // Set the four most significant bits (bits 12 through 15) of the time_hi_and_version
        // field to the 4-bit version number. V4 is 0 1 0 0.
        // This modifies b[7] not b[6] because time_hi_and_version is stored as short type
        // in .NET like below:
        // (short) ((int) b[7] << 8 | (int) b[6])
        b[7] = (byte)((b[7] & 0x0f) | 0x40);

        // Set the two most significant bits (bits 7 and 6) of the clock_seq_hi_and_reserved
        // to one and zero, respectively.
        b[8] = (byte)((b[8] & 0x3f) | 0x80);

        return new Guid(b);
    }
}
