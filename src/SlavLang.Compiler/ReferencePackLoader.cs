using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;

namespace SlavLang.Compiler;

public static class ReferencePackLoader
{
    private static IReadOnlyList<MetadataReference>? cached;
    private static readonly object Sync = new();

    public static IReadOnlyList<MetadataReference> Load()
    {
        lock (Sync)
        {
            return cached ??= LoadCore();
        }
    }

    private static IReadOnlyList<MetadataReference> LoadCore()
    {
        var owner = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var names = owner.GetManifestResourceNames()
            .Where(static name => name.StartsWith("SlavLang.ReferencePack.", StringComparison.Ordinal) &&
                name.EndsWith(".dll", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (names.Length > 0)
        {
            return names.Select(name =>
            {
                using var stream = owner.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"SLAVC2001: Missing reference resource '{name}'.");
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return MetadataReference.CreateFromImage(memory.ToArray());
            }).ToArray();
        }

        var runtimeDirectory = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
        var dotnetRoot = runtimeDirectory.Parent?.Parent?.Parent?.FullName;
        var packRoot = dotnetRoot is null
            ? null
            : Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
        var refPath = packRoot is null || !Directory.Exists(packRoot)
            ? null
            : Directory.GetDirectories(packRoot)
                .OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => Path.Combine(path, "ref", "net10.0"))
                .FirstOrDefault(Directory.Exists);
        if (refPath is null)
        {
            throw new InvalidOperationException(
                "SLAVC2002: Embedded net10.0 reference pack is unavailable.");
        }

        return Directory.GetFiles(refPath, "*.dll")
            .Order(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
