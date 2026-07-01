# Appended payload spike

Date: 2026-07-01  
Runtime: .NET 10.0.8  
RID tested: win-x64

A self-contained single-file RuntimeHost was published and used as the byte prefix
of a RusPack executable. A Brotli-compressed managed assembly, JSON manifest, and
fixed footer were appended. The resulting 73,455,164-byte executable started,
loaded the assembly from memory, printed Cyrillic output, and returned exit code 0.
Rename-independent lookup is implemented through `Environment.ProcessPath`.

The release gate still needs execution on a clean Windows VM without .NET and
explicit 1 byte, 1 KiB, 1 MiB, and 10 MiB padding cases. Authenticode, Linux, and
macOS signing/Gatekeeper checks were not performed in this Windows environment and
must not be marked complete.
