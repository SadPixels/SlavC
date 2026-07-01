# ADR 0001: Runtime host with appended payload

Status: accepted

## Context

`rusc собрать` must produce one RID-specific executable without invoking the .NET SDK,
MSBuild, NuGet, or user code. Editing PE, ELF, and Mach-O structures independently
would create three security-sensitive writers.

## Decision

Release tooling publishes and tests a self-contained, single-file RuntimeHost for
each supported RID. The compiler copies that immutable template and appends a
platform-neutral RusPack container. A fixed footer at end-of-file locates and
authenticates the manifest and payload. Platform signing is performed only after
packing.

Appended bytes are a compatibility assumption, not a runtime contract. Every new
.NET runtime patch must pass the appended-payload gate for every RID before its
template is released. If a runtime fails, `IRuntimeImageWriter` permits replacing
the strategy with the official host-model bundler or a platform section/resource;
sidecar files are not an accepted fallback.

## Versioning

Language, compiler, RusPack format, RuntimeHost protocol, TFM, and runtime patch
versions evolve independently. Unknown RusPack major versions are rejected.
Newer minor versions are accepted only when all required feature flags are known.
