using Xunit;

namespace SlavLang.RuntimeHost.Tests;

public sealed class EntryPointInvokerTests
{
    [Fact]
    public void PublicInvokerApiIsAvailable()
    {
        Assert.NotNull(typeof(EntryPointInvoker).GetMethod(nameof(EntryPointInvoker.InvokeAsync)));
    }
}
