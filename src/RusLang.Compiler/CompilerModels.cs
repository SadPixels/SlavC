namespace RusLang.Compiler;

public enum RusDiagnosticSeverity { Info, Warning, Error }

public sealed record RusDiagnostic(
    string Code,
    RusDiagnosticSeverity Severity,
    string File,
    int Line,
    int Column,
    string Message);

public sealed record CompilationArtifact(
    byte[] MainAssemblyBytes,
    byte[]? PortablePdbBytes,
    IReadOnlyList<RusDiagnostic> Diagnostics,
    string AssemblyIdentity,
    string GeneratedSource);

public sealed record CompilationRequest(
    string SourcePath,
    string AssemblyName,
    bool Debug,
    IReadOnlyList<string> References);
