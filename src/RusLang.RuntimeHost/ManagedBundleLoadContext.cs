using System.Reflection;
using System.Runtime.Loader;
using RusLang.Pack.Format;

namespace RusLang.RuntimeHost;

public sealed class ManagedBundleLoadContext : AssemblyLoadContext
{
    private readonly RusPackReader reader;
    private readonly Dictionary<string, RusPackEntry> assemblies;
    private readonly HashSet<string> resolving = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool trace = Environment.GetEnvironmentVariable("RUSLANG_HOST_TRACE") == "1";

    public ManagedBundleLoadContext(RusPackReader reader)
        : base("RusLang.ManagedBundle", isCollectible: false)
    {
        this.reader = reader;
        assemblies = reader.Manifest.Entries
            .Where(static entry =>
                entry.Kind is RusPackEntryKind.MainAssembly or RusPackEntryKind.ManagedAssembly)
            .ToDictionary(
                static entry => new AssemblyName(entry.AssemblyName!).Name!,
                StringComparer.OrdinalIgnoreCase);
    }

    public Assembly LoadEntryAssembly()
    {
        var entry = reader.Entries[reader.Manifest.EntryAssembly];
        var pe = reader.ReadEntry(entry.Name);
        var pdbEntry = reader.Manifest.Entries.FirstOrDefault(candidate =>
            candidate.Kind == RusPackEntryKind.PortablePdb &&
            string.Equals(
                Path.GetFileNameWithoutExtension(candidate.Name),
                Path.GetFileNameWithoutExtension(entry.Name),
                StringComparison.OrdinalIgnoreCase));
        using var peStream = new MemoryStream(pe, writable: false);
        if (pdbEntry is null)
        {
            return LoadFromStream(peStream);
        }

        using var pdbStream = new MemoryStream(reader.ReadEntry(pdbEntry.Name), writable: false);
        return LoadFromStream(peStream, pdbStream);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name;
        if (simpleName is null || IsFrameworkAssembly(simpleName) ||
            !assemblies.TryGetValue(simpleName, out var entry))
        {
            return null;
        }

        var packagedIdentity = new AssemblyName(entry.AssemblyName!);
        if (!IdentityMatches(assemblyName, packagedIdentity))
        {
            return null;
        }

        lock (resolving)
        {
            if (!resolving.Add(simpleName))
            {
                throw new FileLoadException($"Recursive dependency resolution for '{assemblyName}'.");
            }
        }

        try
        {
            Trace($"loading {packagedIdentity}");
            using var stream = new MemoryStream(reader.ReadEntry(entry.Name), writable: false);
            return LoadFromStream(stream);
        }
        finally
        {
            lock (resolving)
            {
                resolving.Remove(simpleName);
            }
        }
    }

    private static bool IdentityMatches(AssemblyName requested, AssemblyName packaged)
    {
        if (!string.Equals(requested.Name, packaged.Name, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                requested.CultureName ?? string.Empty,
                packaged.CultureName ?? string.Empty,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestedToken = requested.GetPublicKeyToken() ?? [];
        var packagedToken = packaged.GetPublicKeyToken() ?? [];
        if (!requestedToken.SequenceEqual(packagedToken))
        {
            return false;
        }

        return requested.Version is null || packaged.Version is null ||
            packaged.Version >= requested.Version;
    }

    private static bool IsFrameworkAssembly(string name) =>
        name is "mscorlib" or "netstandard" ||
        name.StartsWith("System", StringComparison.Ordinal) ||
        name.StartsWith("Microsoft.", StringComparison.Ordinal);

    private void Trace(string message)
    {
        if (trace)
        {
            Console.Error.WriteLine($"[RusLang host] {message}");
        }
    }
}
