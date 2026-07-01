# RusLang compiler prototype

This repository implements the Windows x64 MVP path from `Tasks.md`: deterministic
in-memory Roslyn compilation, RusPack v1, a self-contained RuntimeHost, and a
self-contained `rusc` that embeds both the .NET 10 reference pack and host template.

Build the release artifacts:

```powershell
./eng/build.ps1
```

Compile and run a program:

```powershell
./artifacts/rusc/win-x64/rusc.exe build ./examples/hello.rus
./examples/hello.exe
```

The initial source syntax is line-oriented:

```text
печать "текст"
ошибка "текст"
выход 5
```

Prefix a file with `#csharp` to exercise the full bootstrap/entry-point pipeline
with C# while the RusLang parser and semantics are expanded.
