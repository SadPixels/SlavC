using System.IO.Compression;
using System.Security.Cryptography;
using RusLang.Pack.Format;

namespace RusLang.Pack.Writer;

public sealed class RusPackWriter : IRuntimeImageWriter
{
    public void Write(RusPackWriteRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var outputPath = Path.GetFullPath(NormalizeOutputName(request.OutputPath, request.TargetRid));
        var hostPath = Path.GetFullPath(request.HostTemplatePath);
        if (string.Equals(outputPath, hostPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Output must not overwrite the host template.", nameof(request));
        }

        if (File.Exists(outputPath) && !request.Force)
        {
            throw new IOException(
                $"Выходной файл «{outputPath}» уже существует. " +
                "Для замены используйте --перезаписать.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var temporaryPath = outputPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            WriteTemporary(request, hostPath, temporaryPath, cancellationToken);
            using (var reader = RusPackReader.OpenExecutable(temporaryPath))
            {
                if (!string.Equals(reader.Manifest.TargetRid, request.TargetRid, StringComparison.Ordinal) ||
                    reader.Manifest.RuntimeHostProtocol != RusPackConstants.RuntimeHostProtocol)
                {
                    throw new RusPackFormatException("RUSP1100: Self-verification metadata mismatch.");
                }

                reader.VerifyAllEntries();
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    temporaryPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            File.Move(temporaryPath, outputPath, request.Force);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static void WriteTemporary(
        RusPackWriteRequest request,
        string hostPath,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        using var output = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            128 * 1024,
            FileOptions.SequentialScan);
        using (var host = File.OpenRead(hostPath))
        {
            host.CopyTo(output);
        }

        RejectPackedHost(output);
        var payloadOffset = output.Position;
        var entries = new List<RusPackEntry>(request.Entries.Count);
        foreach (var input in request.Entries.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var original = input.Content.Span;
            var stored = request.Compress ? Compress(original) : original.ToArray();
            var compression = request.Compress
                ? RusPackCompression.Brotli
                : RusPackCompression.None;
            var offset = output.Position;
            output.Write(stored);
            entries.Add(new RusPackEntry(
                input.Name,
                input.Kind,
                input.AssemblyName,
                input.Culture,
                offset,
                stored.LongLength,
                original.Length,
                compression,
                Convert.ToHexStringLower(SHA256.HashData(original)),
                input.Optional));
        }

        var payloadLength = output.Position - payloadOffset;
        var payloadHash = HashRegion(output, payloadOffset, payloadLength);
        output.Position = payloadOffset + payloadLength;

        var flags = request.Compress ? RusPackFlags.EntriesCompressed : RusPackFlags.None;
        if (entries.Any(static entry => entry.Kind == RusPackEntryKind.PortablePdb))
        {
            flags |= RusPackFlags.PortablePdbPresent;
        }

        if (entries.Any(static entry => entry.Kind == RusPackEntryKind.ManagedAssembly))
        {
            flags |= RusPackFlags.ManagedDependenciesPresent;
        }

        var manifest = RusPackManifest.Create(
            request.ApplicationName,
            request.EntryAssembly,
            request.TargetRid,
            entries,
            request.CompilerVersion,
            request.LanguageVersion);
        var manifestBytes = RusPackManifestSerializer.Serialize(manifest);
        var manifestOffset = output.Position;
        output.Write(manifestBytes);

        Span<byte> footerBytes = stackalloc byte[RusPackConstants.FooterSize];
        RusPackFooterCodec.Write(
            new RusPackFooter(
                RusPackVersion.Current,
                flags,
                manifestOffset,
                manifestBytes.LongLength,
                payloadOffset,
                payloadLength,
                payloadHash),
            footerBytes);
        output.Write(footerBytes);
        output.Flush(flushToDisk: true);
    }

    private static byte[] Compress(ReadOnlySpan<byte> content)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            brotli.Write(content);
        }

        return output.ToArray();
    }

    private static byte[] HashRegion(Stream stream, long offset, long length)
    {
        stream.Position = offset;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        var remaining = length;
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

        return hash.GetHashAndReset();
    }

    private static void RejectPackedHost(Stream host)
    {
        if (host.Length < RusPackConstants.FooterSize)
        {
            return;
        }

        Span<byte> magic = stackalloc byte[8];
        host.Position = host.Length - RusPackConstants.FooterSize;
        host.ReadExactly(magic);
        if (magic.SequenceEqual(RusPackConstants.Magic))
        {
            throw new ArgumentException("Host template is already a RusPack executable.");
        }

        host.Position = host.Length;
    }

    private static string NormalizeOutputName(string path, string rid) =>
        rid.StartsWith("win-", StringComparison.Ordinal) &&
        !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? path + ".exe"
            : path;

    private static void ValidateRequest(RusPackWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.HostTemplatePath))
        {
            throw new FileNotFoundException("Runtime host template was not found.", request.HostTemplatePath);
        }

        if (request.Entries.Count is 0 or > RusPackConstants.MaxEntries)
        {
            throw new ArgumentException("Invalid number of entries.", nameof(request));
        }

        if (request.Entries.Count(static entry => entry.Kind == RusPackEntryKind.MainAssembly) != 1 ||
            !request.Entries.Any(entry =>
                entry.Kind == RusPackEntryKind.MainAssembly &&
                string.Equals(entry.Name, request.EntryAssembly, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Exactly one matching main assembly is required.", nameof(request));
        }
    }
}
