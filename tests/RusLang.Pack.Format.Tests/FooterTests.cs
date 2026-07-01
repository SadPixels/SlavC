using RusLang.Pack.Format;
using Xunit;

namespace RusLang.Pack.Format.Tests;

public sealed class FooterTests
{
    [Fact]
    public void FooterRoundTrips()
    {
        var expected = new RusPackFooter(
            RusPackVersion.Current,
            RusPackFlags.EntriesCompressed,
            200,
            50,
            100,
            100,
            Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray());
        var bytes = new byte[RusPackConstants.FooterSize];

        RusPackFooterCodec.Write(expected, bytes);
        var actual = RusPackFooterCodec.Read(bytes);

        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.Flags, actual.Flags);
        Assert.Equal(expected.ManifestOffset, actual.ManifestOffset);
        Assert.Equal(expected.PayloadSha256, actual.PayloadSha256);
    }

    [Fact]
    public void InvalidMagicIsRejected()
    {
        var bytes = ValidFooter();
        bytes[0] ^= 0xff;
        Assert.Throws<RusPackFormatException>(() => RusPackFooterCodec.Read(bytes));
    }

    [Fact]
    public void InvalidCrcIsRejected()
    {
        var bytes = ValidFooter();
        bytes[20] ^= 0xff;
        Assert.Throws<RusPackFormatException>(() => RusPackFooterCodec.Read(bytes));
    }

    [Fact]
    public void UnknownMajorVersionIsRejected()
    {
        var bytes = ValidFooter();
        bytes[8] = 2;
        RewriteCrc(bytes);
        Assert.Contains(
            "major version",
            Assert.Throws<RusPackFormatException>(() => RusPackFooterCodec.Read(bytes)).Message);
    }

    private static byte[] ValidFooter()
    {
        var bytes = new byte[RusPackConstants.FooterSize];
        RusPackFooterCodec.Write(
            new RusPackFooter(RusPackVersion.Current, 0, 1, 1, 0, 1, new byte[32]),
            bytes);
        return bytes;
    }

    private static void RewriteCrc(byte[] bytes)
    {
        // The public codec deliberately owns CRC generation. Recreate a valid footer
        // with a foreign major, then copy the version after calculating the CRC via
        // the same standard polynomial locally.
        bytes.AsSpan(RusPackConstants.FooterCrcOffset, 4).Clear();
        uint crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1;
            }
        }

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(RusPackConstants.FooterCrcOffset),
            ~crc);
    }
}
