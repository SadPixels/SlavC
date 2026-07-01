using RusLang.Pack.Format;

namespace RusLang.Pack.Writer;

public sealed record RusPackInputEntry(
    string Name,
    RusPackEntryKind Kind,
    string? AssemblyName,
    string? Culture,
    ReadOnlyMemory<byte> Content,
    bool Optional = false);

public sealed record RusPackWriteRequest(
    string HostTemplatePath,
    string OutputPath,
    string ApplicationName,
    string EntryAssembly,
    string TargetRid,
    IReadOnlyList<RusPackInputEntry> Entries,
    bool Compress = true,
    bool Force = false,
    string CompilerVersion = "0.1.0",
    string LanguageVersion = "0.1");

public interface IRuntimeImageWriter
{
    void Write(RusPackWriteRequest request, CancellationToken cancellationToken = default);
}
