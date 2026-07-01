using System.Globalization;

namespace SlavLang.Pack.Format;

public static class SlavPackManifestValidator
{
    public static void ValidateShape(SlavPackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        RequireText(manifest.Format, "format");
        RequireText(manifest.ApplicationName, "applicationName");
        RequireText(manifest.EntryAssembly, "entryAssembly");
        RequireText(manifest.TargetFramework, "targetFramework");
        RequireText(manifest.TargetRid, "targetRid");
        RequireText(manifest.CompilerVersion, "compilerVersion");
        RequireText(manifest.LanguageVersion, "languageVersion");

        if (manifest.Format != SlavPackVersion.Current.ToString())
        {
            throw new SlavPackFormatException($"SLAP1020: Unsupported manifest format '{manifest.Format}'.");
        }

        if (manifest.RuntimeHostProtocol != SlavPackConstants.RuntimeHostProtocol)
        {
            throw new SlavPackFormatException(
                $"SLAP1021: Unsupported RuntimeHost protocol {manifest.RuntimeHostProtocol}.");
        }

        if (manifest.Entries.Count is 0 or > SlavPackConstants.MaxEntries)
        {
            throw new SlavPackFormatException("SLAP1022: Invalid number of manifest entries.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mainCount = 0;
        foreach (var entry in manifest.Entries)
        {
            ValidateEntry(entry);
            if (!names.Add(entry.Name))
            {
                throw new SlavPackFormatException($"SLAP1023: Duplicate entry name '{entry.Name}'.");
            }

            if (entry.Kind == SlavPackEntryKind.MainAssembly)
            {
                mainCount++;
            }

            if (entry.Kind is SlavPackEntryKind.MainAssembly or SlavPackEntryKind.ManagedAssembly &&
                entry.AssemblyName is not null &&
                !identities.Add(entry.AssemblyName))
            {
                throw new SlavPackFormatException(
                    $"SLAP1024: Duplicate assembly identity '{entry.AssemblyName}'.");
            }
        }

        if (mainCount != 1 || !names.Contains(manifest.EntryAssembly))
        {
            throw new SlavPackFormatException("SLAP1025: Manifest must identify exactly one main assembly.");
        }
    }

    public static void ValidateAgainstContainer(
        SlavPackManifest manifest,
        SlavPackFooter footer,
        long fileLength)
    {
        ValidateShape(manifest);
        var payloadEnd = CheckedEnd(footer.PayloadOffset, footer.PayloadLength, "payload");
        if (payloadEnd != footer.ManifestOffset)
        {
            throw new SlavPackFormatException("SLAP1030: Payload and manifest are not contiguous.");
        }

        var manifestEnd = CheckedEnd(footer.ManifestOffset, footer.ManifestLength, "manifest");
        if (manifestEnd != fileLength - SlavPackConstants.FooterSize)
        {
            throw new SlavPackFormatException("SLAP1031: Manifest lies outside the container.");
        }

        var ranges = new List<(long Start, long End, string Name)>(manifest.Entries.Count);
        foreach (var entry in manifest.Entries)
        {
            var end = CheckedEnd(entry.Offset, entry.StoredLength, $"entry '{entry.Name}'");
            if (entry.Offset < footer.PayloadOffset || end > payloadEnd)
            {
                throw new SlavPackFormatException($"SLAP1032: Entry '{entry.Name}' lies outside payload.");
            }

            ranges.Add((entry.Offset, end, entry.Name));
        }

        ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        for (var index = 1; index < ranges.Count; index++)
        {
            if (ranges[index].Start < ranges[index - 1].End)
            {
                throw new SlavPackFormatException(
                    $"SLAP1033: Entries '{ranges[index - 1].Name}' and '{ranges[index].Name}' overlap.");
            }
        }
    }

    private static void ValidateEntry(SlavPackEntry entry)
    {
        ValidateLogicalName(entry.Name);
        if (entry.Offset < 0 || entry.StoredLength < 0 || entry.OriginalLength < 0 ||
            entry.StoredLength > SlavPackConstants.MaxStoredEntrySize ||
            entry.OriginalLength > SlavPackConstants.MaxOriginalEntrySize)
        {
            throw new SlavPackFormatException($"SLAP1040: Invalid size or offset for '{entry.Name}'.");
        }

        if (entry.Compression == SlavPackCompression.None &&
            entry.StoredLength != entry.OriginalLength)
        {
            throw new SlavPackFormatException(
                $"SLAP1041: Uncompressed entry '{entry.Name}' has inconsistent lengths.");
        }

        if (entry.Sha256.Length != 64 ||
            !entry.Sha256.All(static value => char.IsAsciiHexDigit(value)))
        {
            throw new SlavPackFormatException($"SLAP1042: Invalid SHA-256 for '{entry.Name}'.");
        }

        if (entry.Kind is SlavPackEntryKind.MainAssembly or SlavPackEntryKind.ManagedAssembly &&
            string.IsNullOrWhiteSpace(entry.AssemblyName))
        {
            throw new SlavPackFormatException($"SLAP1043: Assembly identity is missing for '{entry.Name}'.");
        }
    }

    private static void ValidateLogicalName(string name)
    {
        RequireText(name, "entry name");
        if (name.Length > 1024 || name.Contains('\\') || name.StartsWith('/') ||
            Path.IsPathRooted(name) ||
            name.Split('/').Any(static part => part is "" or "." or ".."))
        {
            throw new SlavPackFormatException($"SLAP1044: Unsafe logical entry name '{name}'.");
        }
    }

    private static long CheckedEnd(long offset, long length, string description)
    {
        try
        {
            return checked(offset + length);
        }
        catch (OverflowException exception)
        {
            throw new SlavPackFormatException($"SLAP1045: Integer overflow in {description}.", exception);
        }
    }

    private static void RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SlavPackFormatException(
                string.Create(CultureInfo.InvariantCulture, $"SLAP1046: Missing {name}."));
        }
    }
}
