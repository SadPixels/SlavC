using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace RusLang.Pack.Format;

public static class RusPackConstants
{
    public const int FooterSize = 128;
    public const int FooterCrcOffset = 80;
    public const int MaxManifestSize = 16 * 1024 * 1024;
    public const int MaxEntries = 4096;
    public const long MaxStoredEntrySize = 1024L * 1024 * 1024;
    public const long MaxOriginalEntrySize = 2L * 1024 * 1024 * 1024;
    public const int RuntimeHostProtocol = 1;
    public static ReadOnlySpan<byte> Magic => "RUSPACK\0"u8;
}

public readonly record struct RusPackVersion(ushort Major, ushort Minor)
{
    public static RusPackVersion Current => new(1, 0);
    public override string ToString() => $"{Major}.{Minor}";
}

[Flags]
public enum RusPackFlags : uint
{
    None = 0,
    ManifestCompressed = 1 << 0,
    EntriesCompressed = 1 << 1,
    PortablePdbPresent = 1 << 2,
    ManagedDependenciesPresent = 1 << 3,
    NativeDependenciesPresent = 1 << 4,
    SignedManifest = 1 << 5,
    DevelopmentBuild = 1 << 6,
}

public enum RusPackEntryKind
{
    MainAssembly,
    ManagedAssembly,
    PortablePdb,
    Resource,
    SatelliteAssembly,
    NativeLibrary,
}

public enum RusPackCompression
{
    None,
    Brotli,
}

public sealed record RusPackEntry(
    string Name,
    RusPackEntryKind Kind,
    string? AssemblyName,
    string? Culture,
    long Offset,
    long StoredLength,
    long OriginalLength,
    RusPackCompression Compression,
    string Sha256,
    bool Optional = false);

public sealed record RusPackManifest(
    string Format,
    int RuntimeHostProtocol,
    string ApplicationName,
    string EntryAssembly,
    string TargetFramework,
    string TargetRid,
    string CompilerVersion,
    string LanguageVersion,
    IReadOnlyList<RusPackEntry> Entries)
{
    public static RusPackManifest Create(
        string applicationName,
        string entryAssembly,
        string targetRid,
        IEnumerable<RusPackEntry> entries,
        string compilerVersion = "0.1.0",
        string languageVersion = "0.1") =>
        new(
            RusPackVersion.Current.ToString(),
            RusPackConstants.RuntimeHostProtocol,
            applicationName,
            entryAssembly,
            "net10.0",
            targetRid,
            compilerVersion,
            languageVersion,
            new ReadOnlyCollection<RusPackEntry>(entries.ToArray()));
}

public sealed record RusPackFooter(
    RusPackVersion Version,
    RusPackFlags Flags,
    long ManifestOffset,
    long ManifestLength,
    long PayloadOffset,
    long PayloadLength,
    byte[] PayloadSha256);

public sealed class RusPackFormatException : Exception
{
    public RusPackFormatException(string message) : base(message) { }
    public RusPackFormatException(string message, Exception innerException)
        : base(message, innerException) { }
}
