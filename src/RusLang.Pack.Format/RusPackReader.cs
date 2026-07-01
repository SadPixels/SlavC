using System.IO.Compression;
using System.Security.Cryptography;

namespace RusLang.Pack.Format;

public sealed class RusPackReader : IDisposable
{
    private readonly Stream stream;
    private readonly bool ownsStream;
    private readonly Dictionary<string, RusPackEntry> entries;
    private bool disposed;

    private RusPackReader(Stream stream, bool ownsStream)
    {
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("RusPack requires a readable, seekable stream.", nameof(stream));
        }

        this.stream = stream;
        this.ownsStream = ownsStream;
        (Footer, Manifest) = ReadContainer();
        entries = Manifest.Entries.ToDictionary(static entry => entry.Name, StringComparer.Ordinal);
    }

    public RusPackFooter Footer { get; }
    public RusPackManifest Manifest { get; }
    public IReadOnlyDictionary<string, RusPackEntry> Entries => entries;

    public static RusPackReader Open(Stream stream, bool leaveOpen = false) =>
        new(stream, !leaveOpen);

    public static RusPackReader OpenExecutable(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new RusPackReader(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete),
            ownsStream: true);
    }

    public byte[] ReadEntry(string name)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!entries.TryGetValue(name, out var entry))
        {
            throw new KeyNotFoundException($"RusPack entry '{name}' was not found.");
        }

        if (entry.StoredLength > int.MaxValue || entry.OriginalLength > int.MaxValue)
        {
            throw new RusPackFormatException($"RUSP1050: Entry '{name}' is too large for in-memory loading.");
        }

        var stored = new byte[(int)entry.StoredLength];
        lock (stream)
        {
            stream.Position = entry.Offset;
            stream.ReadExactly(stored);
        }

        byte[] content;
        if (entry.Compression == RusPackCompression.None)
        {
            content = stored;
        }
        else if (entry.Compression == RusPackCompression.Brotli)
        {
            try
            {
                using var input = new MemoryStream(stored, writable: false);
                using var brotli = new BrotliStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream((int)entry.OriginalLength);
                brotli.CopyTo(output);
                content = output.ToArray();
            }
            catch (InvalidDataException exception)
            {
                throw new RusPackFormatException(
                    $"RUSP1051: Brotli data for '{name}' is corrupted.", exception);
            }
        }
        else
        {
            throw new RusPackFormatException(
                $"RUSP1052: Unsupported compression for '{name}'.");
        }

        if (content.LongLength != entry.OriginalLength)
        {
            throw new RusPackFormatException(
                $"RUSP1053: Decompressed length mismatch for '{name}'.");
        }

        var actualHash = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new RusPackFormatException($"RUSP1054: SHA-256 mismatch for '{name}'.");
        }

        return content;
    }

    public void VerifyAllEntries()
    {
        foreach (var entry in Manifest.Entries)
        {
            _ = ReadEntry(entry.Name);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (ownsStream)
        {
            stream.Dispose();
        }
    }

    private (RusPackFooter Footer, RusPackManifest Manifest) ReadContainer()
    {
        if (stream.Length < RusPackConstants.FooterSize)
        {
            throw new RusPackFormatException("RUSP1001: File is too short to contain a RusPack footer.");
        }

        Span<byte> footerBytes = stackalloc byte[RusPackConstants.FooterSize];
        stream.Position = stream.Length - RusPackConstants.FooterSize;
        stream.ReadExactly(footerBytes);
        var footer = RusPackFooterCodec.Read(footerBytes);

        if (footer.ManifestLength is < 1 or > RusPackConstants.MaxManifestSize ||
            footer.ManifestOffset < 0 ||
            footer.ManifestOffset > stream.Length - RusPackConstants.FooterSize ||
            footer.ManifestLength > stream.Length - RusPackConstants.FooterSize - footer.ManifestOffset)
        {
            throw new RusPackFormatException("RUSP1031: Manifest lies outside the container.");
        }

        ValidatePayloadHash(footer);
        var manifestBytes = new byte[(int)footer.ManifestLength];
        stream.Position = footer.ManifestOffset;
        stream.ReadExactly(manifestBytes);
        var manifest = RusPackManifestSerializer.Deserialize(manifestBytes);
        RusPackManifestValidator.ValidateAgainstContainer(manifest, footer, stream.Length);
        return (footer, manifest);
    }

    private void ValidatePayloadHash(RusPackFooter footer)
    {
        if (footer.PayloadOffset < 0 || footer.PayloadLength < 0 ||
            footer.PayloadOffset > stream.Length ||
            footer.PayloadLength > stream.Length - footer.PayloadOffset)
        {
            throw new RusPackFormatException("RUSP1055: Payload lies outside the container.");
        }

        stream.Position = footer.PayloadOffset;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        var remaining = footer.PayloadLength;
        while (remaining > 0)
        {
            var count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (count == 0)
            {
                throw new EndOfStreamException();
            }

            hash.AppendData(buffer, 0, count);
            remaining -= count;
        }

        if (!CryptographicOperations.FixedTimeEquals(
            footer.PayloadSha256,
            hash.GetHashAndReset()))
        {
            throw new RusPackFormatException("RUSP1056: Payload SHA-256 mismatch.");
        }
    }
}
