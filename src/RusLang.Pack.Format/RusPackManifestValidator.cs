using System.Globalization;

namespace RusLang.Pack.Format;

public static class RusPackManifestValidator
{
    public static void ValidateShape(RusPackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        RequireText(manifest.Format, "format");
        RequireText(manifest.ApplicationName, "applicationName");
        RequireText(manifest.EntryAssembly, "entryAssembly");
        RequireText(manifest.TargetFramework, "targetFramework");
        RequireText(manifest.TargetRid, "targetRid");
        RequireText(manifest.CompilerVersion, "compilerVersion");
        RequireText(manifest.LanguageVersion, "languageVersion");

        if (manifest.Format != RusPackVersion.Current.ToString())
        {
            throw new RusPackFormatException($"RUSP1020: Unsupported manifest format '{manifest.Format}'.");
        }

        if (manifest.RuntimeHostProtocol != RusPackConstants.RuntimeHostProtocol)
        {
            throw new RusPackFormatException(
                $"RUSP1021: Unsupported RuntimeHost protocol {manifest.RuntimeHostProtocol}.");
        }

        if (manifest.Entries.Count is 0 or > RusPackConstants.MaxEntries)
        {
            throw new RusPackFormatException("RUSP1022: Invalid number of manifest entries.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mainCount = 0;
        foreach (var entry in manifest.Entries)
        {
            ValidateEntry(entry);
            if (!names.Add(entry.Name))
            {
                throw new RusPackFormatException($"RUSP1023: Duplicate entry name '{entry.Name}'.");
            }

            if (entry.Kind == RusPackEntryKind.MainAssembly)
            {
                mainCount++;
            }

            if (entry.Kind is RusPackEntryKind.MainAssembly or RusPackEntryKind.ManagedAssembly &&
                entry.AssemblyName is not null &&
                !identities.Add(entry.AssemblyName))
            {
                throw new RusPackFormatException(
                    $"RUSP1024: Duplicate assembly identity '{entry.AssemblyName}'.");
            }
        }

        if (mainCount != 1 || !names.Contains(manifest.EntryAssembly))
        {
            throw new RusPackFormatException("RUSP1025: Manifest must identify exactly one main assembly.");
        }
    }

    public static void ValidateAgainstContainer(
        RusPackManifest manifest,
        RusPackFooter footer,
        long fileLength)
    {
        ValidateShape(manifest);
        var payloadEnd = CheckedEnd(footer.PayloadOffset, footer.PayloadLength, "payload");
        if (payloadEnd != footer.ManifestOffset)
        {
            throw new RusPackFormatException("RUSP1030: Payload and manifest are not contiguous.");
        }

        var manifestEnd = CheckedEnd(footer.ManifestOffset, footer.ManifestLength, "manifest");
        if (manifestEnd != fileLength - RusPackConstants.FooterSize)
        {
            throw new RusPackFormatException("RUSP1031: Manifest lies outside the container.");
        }

        var ranges = new List<(long Start, long End, string Name)>(manifest.Entries.Count);
        foreach (var entry in manifest.Entries)
        {
            var end = CheckedEnd(entry.Offset, entry.StoredLength, $"entry '{entry.Name}'");
            if (entry.Offset < footer.PayloadOffset || end > payloadEnd)
            {
                throw new RusPackFormatException($"RUSP1032: Entry '{entry.Name}' lies outside payload.");
            }

            ranges.Add((entry.Offset, end, entry.Name));
        }

        ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        for (var index = 1; index < ranges.Count; index++)
        {
            if (ranges[index].Start < ranges[index - 1].End)
            {
                throw new RusPackFormatException(
                    $"RUSP1033: Entries '{ranges[index - 1].Name}' and '{ranges[index].Name}' overlap.");
            }
        }
    }

    private static void ValidateEntry(RusPackEntry entry)
    {
        ValidateLogicalName(entry.Name);
        if (entry.Offset < 0 || entry.StoredLength < 0 || entry.OriginalLength < 0 ||
            entry.StoredLength > RusPackConstants.MaxStoredEntrySize ||
            entry.OriginalLength > RusPackConstants.MaxOriginalEntrySize)
        {
            throw new RusPackFormatException($"RUSP1040: Invalid size or offset for '{entry.Name}'.");
        }

        if (entry.Compression == RusPackCompression.None &&
            entry.StoredLength != entry.OriginalLength)
        {
            throw new RusPackFormatException(
                $"RUSP1041: Uncompressed entry '{entry.Name}' has inconsistent lengths.");
        }

        if (entry.Sha256.Length != 64 ||
            !entry.Sha256.All(static value => char.IsAsciiHexDigit(value)))
        {
            throw new RusPackFormatException($"RUSP1042: Invalid SHA-256 for '{entry.Name}'.");
        }

        if (entry.Kind is RusPackEntryKind.MainAssembly or RusPackEntryKind.ManagedAssembly &&
            string.IsNullOrWhiteSpace(entry.AssemblyName))
        {
            throw new RusPackFormatException($"RUSP1043: Assembly identity is missing for '{entry.Name}'.");
        }
    }

    private static void ValidateLogicalName(string name)
    {
        RequireText(name, "entry name");
        if (name.Length > 1024 || name.Contains('\\') || name.StartsWith('/') ||
            Path.IsPathRooted(name) ||
            name.Split('/').Any(static part => part is "" or "." or ".."))
        {
            throw new RusPackFormatException($"RUSP1044: Unsafe logical entry name '{name}'.");
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
            throw new RusPackFormatException($"RUSP1045: Integer overflow in {description}.", exception);
        }
    }

    private static void RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RusPackFormatException(
                string.Create(CultureInfo.InvariantCulture, $"RUSP1046: Missing {name}."));
        }
    }
}
