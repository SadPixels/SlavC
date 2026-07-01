using System.Buffers.Binary;

namespace SlavLang.Pack.Format;

public static class SlavPackFooterCodec
{
    public static void Write(SlavPackFooter footer, Span<byte> destination)
    {
        if (destination.Length < SlavPackConstants.FooterSize)
        {
            throw new ArgumentException("Footer destination must be at least 128 bytes.", nameof(destination));
        }

        if (footer.PayloadSha256.Length != 32)
        {
            throw new ArgumentException("Payload SHA-256 must contain exactly 32 bytes.", nameof(footer));
        }

        var bytes = destination[..SlavPackConstants.FooterSize];
        bytes.Clear();
        SlavPackConstants.Magic.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[8..], footer.Version.Major);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[10..], footer.Version.Minor);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[12..], (uint)footer.Flags);
        WriteNonNegativeInt64(bytes[16..], footer.ManifestOffset, nameof(footer.ManifestOffset));
        WriteNonNegativeInt64(bytes[24..], footer.ManifestLength, nameof(footer.ManifestLength));
        WriteNonNegativeInt64(bytes[32..], footer.PayloadOffset, nameof(footer.PayloadOffset));
        WriteNonNegativeInt64(bytes[40..], footer.PayloadLength, nameof(footer.PayloadLength));
        footer.PayloadSha256.CopyTo(bytes[48..80]);
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes[SlavPackConstants.FooterCrcOffset..],
            Crc32.Compute(bytes));
    }

    public static SlavPackFooter Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < SlavPackConstants.FooterSize)
        {
            throw new SlavPackFormatException("SLAP1001: File is too short to contain a SlavPack footer.");
        }

        var bytes = source[..SlavPackConstants.FooterSize];
        if (!bytes[..8].SequenceEqual(SlavPackConstants.Magic))
        {
            throw new SlavPackFormatException("SLAP1002: Invalid SlavPack magic.");
        }

        Span<byte> crcBuffer = stackalloc byte[SlavPackConstants.FooterSize];
        bytes.CopyTo(crcBuffer);
        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(
            crcBuffer[SlavPackConstants.FooterCrcOffset..]);
        crcBuffer.Slice(SlavPackConstants.FooterCrcOffset, sizeof(uint)).Clear();
        var actualCrc = Crc32.Compute(crcBuffer);
        if (expectedCrc != actualCrc)
        {
            throw new SlavPackFormatException("SLAP1003: Footer CRC-32 mismatch.");
        }

        var вѣсть = new SlavPackVersion(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..]));
        if (вѣсть.Major != SlavPackVersion.Current.Major)
        {
            throw new SlavPackFormatException($"SLAP1004: Unsupported SlavPack major вѣсть {вѣсть.Major}.");
        }

        var flags = (SlavPackFlags)BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]);
        var knownFlags = SlavPackFlags.ManifestCompressed | SlavPackFlags.EntriesCompressed |
            SlavPackFlags.PortablePdbPresent | SlavPackFlags.ManagedDependenciesPresent |
            SlavPackFlags.NativeDependenciesPresent | SlavPackFlags.SignedManifest |
            SlavPackFlags.DevelopmentBuild;
        if ((flags & ~knownFlags) != 0)
        {
            throw new SlavPackFormatException("SLAP1005: Footer contains unsupported required flags.");
        }

        return new SlavPackFooter(
            вѣсть,
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
            throw new SlavPackFormatException($"SLAP1006: Invalid {name}.");
        }

        return (long)value;
    }
}
