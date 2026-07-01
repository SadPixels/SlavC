using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SlavLang.Pack.Format;

public static class SlavPackConstants
{
    public const int FooterSize = 128;
    public const int FooterCrcOffset = 80;
    public const int MaxManifestSize = 16 * 1024 * 1024;
    public const int MaxEntries = 4096;
    public const long MaxStoredEntrySize = 1024L * 1024 * 1024;
    public const long MaxOriginalEntrySize = 2L * 1024 * 1024 * 1024;
    public const int RuntimeHostProtocol = 1;
    public static ReadOnlySpan<byte> Magic => "SLAVPAK\0"u8;
}

public readonly record struct SlavPackVersion(ushort Major, ushort Minor)
{
    public static SlavPackVersion Current => new(1, 0);
    public override string ToString() => $"{Major}.{Minor}";
}

[Flags]
public enum SlavPackFlags : uint
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

public enum SlavPackEntryKind
{
    MainAssembly,
    ManagedAssembly,
    PortablePdb,
    Resource,
    SatelliteAssembly,
    NativeLibrary,
}

public enum SlavPackCompression
{
    None,
    Brotli,
}

public sealed record SlavPackEntry(
    string Name,
    SlavPackEntryKind Kind,
    string? AssemblyName,
    string? Culture,
    long Offset,
    long StoredLength,
    long OriginalLength,
    SlavPackCompression Compression,
    string Sha256,
    bool Optional = false);

public sealed record SlavPackManifest(
    string Format,
    int RuntimeHostProtocol,
    string ApplicationName,
    string EntryAssembly,
    string TargetFramework,
    string TargetRid,
    string CompilerVersion,
    string LanguageVersion,
    IReadOnlyList<SlavPackEntry> Entries)
{
    public static SlavPackManifest Create(
        string applicationName,
        string entryAssembly,
        string targetRid,
        IEnumerable<SlavPackEntry> entries,
        string compilerVersion = "0.1.0",
        string languageVersion = "0.1") =>
        new(
            SlavPackVersion.Current.ToString(),
            SlavPackConstants.RuntimeHostProtocol,
            applicationName,
            entryAssembly,
            "net10.0",
            targetRid,
            compilerVersion,
            languageVersion,
            new ReadOnlyCollection<SlavPackEntry>(entries.ToArray()));
}

public sealed record SlavPackFooter(
    SlavPackVersion Version,
    SlavPackFlags Flags,
    long ManifestOffset,
    long ManifestLength,
    long PayloadOffset,
    long PayloadLength,
    byte[] PayloadSha256);

public sealed class SlavPackFormatException : Exception
{
    public SlavPackFormatException(string message) : base(message) { }
    public SlavPackFormatException(string message, Exception innerException)
        : base(message, innerException) { }
}
