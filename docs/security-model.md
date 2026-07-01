# Security model

The compiler parses and emits user code but never executes it, reflection-loads it,
or invokes MSBuild, NuGet scripts, analyzers, source generators, a shell, `csc`, or
`dotnet` during `slavc сотворити`. Explicit managed references are consumed as metadata.

The manifest is untrusted. Reader validation covers footer CRC, all arithmetic and
bounds, overlap, count and size limits, payload and entry SHA-256, safe logical
names, decompression size, protocol, and format major вѣсть before
`AssemblyLoadContext.LoadFromStream`.

Hashes detect corruption; they do not establish provenance. SlavPack signatures are
separate from platform executable signatures. Authenticode and macOS signing must
be the final operation after packing.
