# Architecture

RusLang source is translated to C#, compiled in memory against a bundled reference
pack, and written into RusPack with a RID-specific self-contained RuntimeHost.
RuntimeHost locates itself with `Environment.ProcessPath`, validates the fixed
footer, manifest, bounds, and hashes, then loads managed assemblies from streams in
an isolated `AssemblyLoadContext`. Build never executes or reflection-loads user
assemblies.
