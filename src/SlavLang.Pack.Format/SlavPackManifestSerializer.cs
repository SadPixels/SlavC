using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlavLang.Pack.Format;

public static class SlavPackManifestSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    public static byte[] Serialize(SlavPackManifest manifest)
    {
        SlavPackManifestValidator.ValidateShape(manifest);
        return JsonSerializer.SerializeToUtf8Bytes(manifest, Options);
    }

    public static SlavPackManifest Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length > SlavPackConstants.MaxManifestSize)
        {
            throw new SlavPackFormatException("SLAP1010: Manifest exceeds the 16 MiB limit.");
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<SlavPackManifest>(utf8Json, Options)
                ?? throw new SlavPackFormatException("SLAP1011: Manifest is null.");
            SlavPackManifestValidator.ValidateShape(manifest);
            return manifest;
        }
        catch (SlavPackFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new SlavPackFormatException("SLAP1012: Manifest is not valid SlavPack JSON.", exception);
        }
    }
}
