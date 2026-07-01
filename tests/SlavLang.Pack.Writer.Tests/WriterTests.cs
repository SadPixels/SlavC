using SlavLang.Pack.Format;
using Xunit;

namespace SlavLang.Pack.Writer.Tests;

public sealed class WriterTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "SlavLangTests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WrittenContainerSelfVerifies(bool compress)
    {
        Directory.CreateDirectory(directory);
        var host = Path.Combine(directory, "host.exe");
        var output = Path.Combine(directory, "app");
        File.WriteAllBytes(host, Enumerable.Range(0, 257).Select(static x => (byte)x).ToArray());
        var content = "managed assembly placeholder"u8.ToArray();
        var writer = new SlavPackWriter();

        writer.Write(new SlavPackWriteRequest(
            host,
            output,
            "Тест",
            "Тест.dll",
            "win-x64",
            [
                new(
                    "Тест.dll",
                    SlavPackEntryKind.MainAssembly,
                    "Тест, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                    null,
                    content),
            ],
            Compress: compress),
            TestContext.Current.CancellationToken);

        using var reader = SlavPackReader.OpenExecutable(output + ".exe");
        Assert.Equal(content, reader.ReadEntry("Тест.dll"));
        Assert.Equal("Тест", reader.Manifest.ApplicationName);
    }

    [Fact]
    public void CorruptedPayloadIsRejected()
    {
        Directory.CreateDirectory(directory);
        var host = Path.Combine(directory, "host.exe");
        var output = Path.Combine(directory, "app.exe");
        File.WriteAllBytes(host, [1, 2, 3]);
        new SlavPackWriter().Write(new SlavPackWriteRequest(
            host,
            output,
            "App",
            "App.dll",
            "win-x64",
            [new("App.dll", SlavPackEntryKind.MainAssembly, "App, Version=1.0.0.0", null, new byte[100])],
            Compress: false),
            TestContext.Current.CancellationToken);
        var bytes = File.ReadAllBytes(output);
        bytes[0] ^= 0xff;
        File.WriteAllBytes(output, bytes);

        // Host bytes are intentionally outside the authenticated payload.
        using var valid = SlavPackReader.OpenExecutable(output);
        valid.VerifyAllEntries();
        valid.Dispose();
        bytes = File.ReadAllBytes(output);
        bytes[checked((int)valid.Footer.PayloadOffset)] ^= 0xff;
        File.WriteAllBytes(output, bytes);
        Assert.Throws<SlavPackFormatException>(() => SlavPackReader.OpenExecutable(output));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
