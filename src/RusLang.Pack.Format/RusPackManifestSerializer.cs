using System.Text.Json;
using System.Text.Json.Serialization;

namespace RusLang.Pack.Format;

public static class RusPackManifestSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    public static byte[] Serialize(RusPackManifest manifest)
    {
        RusPackManifestValidator.ValidateShape(manifest);
        return JsonSerializer.SerializeToUtf8Bytes(manifest, Options);
    }

    public static RusPackManifest Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length > RusPackConstants.MaxManifestSize)
        {
            throw new RusPackFormatException("RUSP1010: Manifest exceeds the 16 MiB limit.");
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<RusPackManifest>(utf8Json, Options)
                ?? throw new RusPackFormatException("RUSP1011: Manifest is null.");
            RusPackManifestValidator.ValidateShape(manifest);
            return manifest;
        }
        catch (RusPackFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new RusPackFormatException("RUSP1012: Manifest is not valid RusPack JSON.", exception);
        }
    }
}
