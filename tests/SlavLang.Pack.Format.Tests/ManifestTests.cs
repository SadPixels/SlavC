using SlavLang.Pack.Format;
using Xunit;

namespace SlavLang.Pack.Format.Tests;

public sealed class ManifestTests
{
    [Fact]
    public void UnicodeManifestRoundTripsDeterministically()
    {
        var entry = new SlavPackEntry(
            "Сказание.dll",
            SlavPackEntryKind.MainAssembly,
            "Сказание, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            null,
            100,
            4,
            4,
            SlavPackCompression.None,
            new string('a', 64));
        var manifest = SlavPackManifest.Create("Сказание", entry.Name, "win-x64", [entry]);

        var first = SlavPackManifestSerializer.Serialize(manifest);
        var actual = SlavPackManifestSerializer.Deserialize(first);
        var second = SlavPackManifestSerializer.Serialize(actual);

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
        var entry = new SlavPackEntry(
            name,
            SlavPackEntryKind.MainAssembly,
            "Bad, Version=1.0.0.0",
            null,
            0,
            1,
            1,
            SlavPackCompression.None,
            new string('a', 64));
        var manifest = SlavPackManifest.Create("Bad", name, "win-x64", [entry]);

        Assert.Throws<SlavPackFormatException>(() => SlavPackManifestSerializer.Serialize(manifest));
    }
}
