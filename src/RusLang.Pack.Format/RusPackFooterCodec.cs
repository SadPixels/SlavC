using System.Buffers.Binary;

namespace RusLang.Pack.Format;

public static class RusPackFooterCodec
{
    public static void Write(RusPackFooter footer, Span<byte> destination)
    {
        if (destination.Length < RusPackConstants.FooterSize)
        {
            throw new ArgumentException("Footer destination must be at least 128 bytes.", nameof(destination));
        }

        if (footer.PayloadSha256.Length != 32)
        {
            throw new ArgumentException("Payload SHA-256 must contain exactly 32 bytes.", nameof(footer));
        }

        var bytes = destination[..RusPackConstants.FooterSize];
        bytes.Clear();
        RusPackConstants.Magic.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[8..], footer.Version.Major);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[10..], footer.Version.Minor);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[12..], (uint)footer.Flags);
        WriteNonNegativeInt64(bytes[16..], footer.ManifestOffset, nameof(footer.ManifestOffset));
        WriteNonNegativeInt64(bytes[24..], footer.ManifestLength, nameof(footer.ManifestLength));
        WriteNonNegativeInt64(bytes[32..], footer.PayloadOffset, nameof(footer.PayloadOffset));
        WriteNonNegativeInt64(bytes[40..], footer.PayloadLength, nameof(footer.PayloadLength));
        footer.PayloadSha256.CopyTo(bytes[48..80]);
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes[RusPackConstants.FooterCrcOffset..],
            Crc32.Compute(bytes));
    }

    public static RusPackFooter Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < RusPackConstants.FooterSize)
        {
            throw new RusPackFormatException("RUSP1001: File is too short to contain a RusPack footer.");
        }

        var bytes = source[..RusPackConstants.FooterSize];
        if (!bytes[..8].SequenceEqual(RusPackConstants.Magic))
        {
            throw new RusPackFormatException("RUSP1002: Invalid RusPack magic.");
        }

        Span<byte> crcBuffer = stackalloc byte[RusPackConstants.FooterSize];
        bytes.CopyTo(crcBuffer);
        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(
            crcBuffer[RusPackConstants.FooterCrcOffset..]);
        crcBuffer.Slice(RusPackConstants.FooterCrcOffset, sizeof(uint)).Clear();
        var actualCrc = Crc32.Compute(crcBuffer);
        if (expectedCrc != actualCrc)
        {
            throw new RusPackFormatException("RUSP1003: Footer CRC-32 mismatch.");
        }

        var version = new RusPackVersion(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..]));
        if (version.Major != RusPackVersion.Current.Major)
        {
            throw new RusPackFormatException($"RUSP1004: Unsupported RusPack major version {version.Major}.");
        }

        var flags = (RusPackFlags)BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]);
        var knownFlags = RusPackFlags.ManifestCompressed | RusPackFlags.EntriesCompressed |
            RusPackFlags.PortablePdbPresent | RusPackFlags.ManagedDependenciesPresent |
            RusPackFlags.NativeDependenciesPresent | RusPackFlags.SignedManifest |
            RusPackFlags.DevelopmentBuild;
        if ((flags & ~knownFlags) != 0)
        {
            throw new RusPackFormatException("RUSP1005: Footer contains unsupported required flags.");
        }

        return new RusPackFooter(
            version,
            flags,
            ReadInt64(bytes[16..], "manifest offset"),
            ReadInt64(bytes[24..], "manifest length"),
            ReadInt64(bytes[32..], "payload offset"),
            ReadInt64(bytes[40..], "payload length"),
            bytes[48..80].ToArray());
    }

    private static void WriteNonNegativeInt64(Span<byte> target, long value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }

        BinaryPrimitives.WriteUInt64LittleEndian(target, (ulong)value);
    }

    private static long ReadInt64(ReadOnlySpan<byte> source, string name)
    {
        var value = BinaryPrimitives.ReadUInt64LittleEndian(source);
        if (value > long.MaxValue)
        {
            throw new RusPackFormatException($"RUSP1006: Invalid {name}.");
        }

        return (long)value;
    }
}
