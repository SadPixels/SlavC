# RuntimeHost

RuntimeHost uses `Environment.ProcessPath`, validates RusPack before allocating or
loading assemblies, and loads managed entries directly from memory. It does not
change the current directory or redirect standard streams. Supported entry-point
returns are `void`, `int`, `Task`, and `Task<int>`, with optional `string[] args`.

User assemblies are stream-loaded, so `Assembly.Location` can be empty. Application
code must use `Environment.ProcessPath` for the executable and
`Path.GetDirectoryName(Environment.ProcessPath)` for its directory; neither is the
same as the current working directory.

Exit codes 120–123 are reserved for host failures. User exit codes pass through.
