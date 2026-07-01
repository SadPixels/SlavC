namespace SlavLang.Compiler;

public enum SlavDiagnosticSeverity { Info, Warning, Error }

public sealed record SlavDiagnostic(
    string Code,
    SlavDiagnosticSeverity Severity,
    string File,
    int Line,
    int Column,
    string Message);

public sealed record CompilationArtifact(
    byte[] MainAssemblyBytes,
    byte[]? PortablePdbBytes,
    IReadOnlyList<SlavDiagnostic> Diagnostics,
    string AssemblyIdentity,
    string GeneratedSource);

public sealed record CompilationRequest(
    string SourcePath,
    string AssemblyName,
    bool Debug,
    IReadOnlyList<string> References);
