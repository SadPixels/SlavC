using SlavLang.Pack.Format;

namespace SlavLang.Pack.Writer;

public sealed record SlavPackInputEntry(
    string Name,
    SlavPackEntryKind Kind,
    string? AssemblyName,
    string? Culture,
    ReadOnlyMemory<byte> Content,
    bool Optional = false);

public sealed record SlavPackWriteRequest(
    string HostTemplatePath,
    string OutputPath,
    string ApplicationName,
    string EntryAssembly,
    string TargetRid,
    IReadOnlyList<SlavPackInputEntry> Entries,
    bool Compress = true,
    bool Force = false,
    string CompilerVersion = "0.1.0",
    string LanguageVersion = "0.1");

public interface IRuntimeImageWriter
{
    void Write(SlavPackWriteRequest request, CancellationToken cancellationToken = default);
}
