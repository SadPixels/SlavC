using RusLang.Pack.Format;
using Xunit;

namespace RusLang.Pack.Format.Tests;

public sealed class ManifestTests
{
    [Fact]
    public void UnicodeManifestRoundTripsDeterministically()
    {
        var entry = new RusPackEntry(
            "Сказание.dll",
            RusPackEntryKind.MainAssembly,
            "Сказание, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            null,
            100,
            4,
            4,
            RusPackCompression.None,
            new string('a', 64));
        var manifest = RusPackManifest.Create("Сказание", entry.Name, "win-x64", [entry]);

        var first = RusPackManifestSerializer.Serialize(manifest);
        var actual = RusPackManifestSerializer.Deserialize(first);
        var second = RusPackManifestSerializer.Serialize(actual);

        Assert.Equal(first, second);
        Assert.Equal("Сказание", actual.ApplicationName);
    }

    [Theory]
    [InlineData("../bad.dll")]
    [InlineData("/bad.dll")]
    [InlineData("dir\\bad.dll")]
    [InlineData("dir//bad.dll")]
    public void UnsafeNamesAreRejected(string name)
    {
        var entry = new RusPackEntry(
            name,
            RusPackEntryKind.MainAssembly,
            "Bad, Version=1.0.0.0",
            null,
            0,
            1,
            1,
            RusPackCompression.None,
            new string('a', 64));
        var manifest = RusPackManifest.Create("Bad", name, "win-x64", [entry]);

        Assert.Throws<RusPackFormatException>(() => RusPackManifestSerializer.Serialize(manifest));
    }
}
