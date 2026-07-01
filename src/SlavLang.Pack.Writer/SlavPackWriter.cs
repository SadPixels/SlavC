using System.IO.Compression;
using System.Security.Cryptography;
using SlavLang.Pack.Format;

namespace SlavLang.Pack.Writer;

public sealed class SlavPackWriter : IRuntimeImageWriter
{
    public void Write(SlavPackWriteRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var outputPath = Path.GetFullPath(NormalizeOutputName(request.OutputPath, request.TargetRid));
        var hostPath = Path.GetFullPath(request.HostTemplatePath);
        if (string.Equals(outputPath, hostPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Исходъ не долженъ переписати основу запуска.", nameof(request));
        }

        if (File.Exists(outputPath) && !request.Force)
        {
            throw new IOException($"Исходъ '{outputPath}' уже есть. Употреби --переписати, дабы замѣнити его.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var temporaryPath = outputPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            WriteTemporary(request, hostPath, temporaryPath, cancellationToken);
            using (var reader = SlavPackReader.OpenExecutable(temporaryPath))
            {
                if (!string.Equals(reader.Manifest.TargetRid, request.TargetRid, StringComparison.Ordinal) ||
                    reader.Manifest.RuntimeHostProtocol != SlavPackConstants.RuntimeHostProtocol)
                {
                    throw new SlavPackFormatException("SLAP1100: Самопровѣрка метаданныхъ не сошлась.");
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
        SlavPackWriteRequest request,
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
        var entries = new List<SlavPackEntry>(request.Entries.Count);
        foreach (var input in request.Entries.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var original = input.Content.Span;
            var stored = request.Compress ? Compress(original) : original.ToArray();
            var compression = request.Compress
                ? SlavPackCompression.Brotli
                : SlavPackCompression.None;
            var offset = output.Position;
            output.Write(stored);
            entries.Add(new SlavPackEntry(
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

        var flags = request.Compress ? SlavPackFlags.EntriesCompressed : SlavPackFlags.None;
        if (entries.Any(static entry => entry.Kind == SlavPackEntryKind.PortablePdb))
        {
            flags |= SlavPackFlags.PortablePdbPresent;
        }

        if (entries.Any(static entry => entry.Kind == SlavPackEntryKind.ManagedAssembly))
        {
            flags |= SlavPackFlags.ManagedDependenciesPresent;
        }

        var manifest = SlavPackManifest.Create(
            request.ApplicationName,
            request.EntryAssembly,
            request.TargetRid,
            entries,
            request.CompilerVersion,
            request.LanguageVersion);
        var manifestBytes = SlavPackManifestSerializer.Serialize(manifest);
        var manifestOffset = output.Position;
        output.Write(manifestBytes);

        Span<byte> footerBytes = stackalloc byte[SlavPackConstants.FooterSize];
        SlavPackFooterCodec.Write(
            new SlavPackFooter(
                SlavPackVersion.Current,
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
        if (host.Length < SlavPackConstants.FooterSize)
        {
            return;
        }

        Span<byte> magic = stackalloc byte[8];
        host.Position = host.Length - SlavPackConstants.FooterSize;
        host.ReadExactly(magic);
        if (magic.SequenceEqual(SlavPackConstants.Magic))
        {
            throw new ArgumentException("Host template is already a SlavPack executable.");
        }

        host.Position = host.Length;
    }

    private static string NormalizeOutputName(string path, string rid) =>
        rid.StartsWith("win-", StringComparison.Ordinal) &&
        !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? path + ".exe"
            : path;

    private static void ValidateRequest(SlavPackWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.HostTemplatePath))
        {
            throw new FileNotFoundException("Runtime host template was not found.", request.HostTemplatePath);
        }

        if (request.Entries.Count is 0 or > SlavPackConstants.MaxEntries)
        {
            throw new ArgumentException("Invalid number of entries.", nameof(request));
        }

        if (request.Entries.Count(static entry => entry.Kind == SlavPackEntryKind.MainAssembly) != 1 ||
            !request.Entries.Any(entry =>
                entry.Kind == SlavPackEntryKind.MainAssembly &&
                string.Equals(entry.Name, request.EntryAssembly, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Exactly one matching main assembly is required.", nameof(request));
        }
    }
}
