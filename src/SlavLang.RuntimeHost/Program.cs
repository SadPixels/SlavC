using SlavLang.Pack.Format;

namespace SlavLang.RuntimeHost;

internal static class Program
{
    private const int MissingProcessPath = 120;
    private const int InvalidContainer = 121;
    private const int ProtocolMismatch = 122;
    private const int HostFailure = 123;

    private static async Task<int> Main(string[] args)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            Console.Error.WriteLine("SLAVH1001: The current executable path is unavailable.");
            return MissingProcessPath;
        }

        try
        {
            using var reader = SlavPackReader.OpenExecutable(processPath);
            if (reader.Manifest.RuntimeHostProtocol != SlavPackConstants.RuntimeHostProtocol)
            {
                Console.Error.WriteLine("SLAVH1002: RuntimeHost protocol mismatch.");
                return ProtocolMismatch;
            }

            var context = new ManagedBundleLoadContext(reader);
            var assembly = context.LoadEntryAssembly();
            return await EntryPointInvoker.InvokeAsync(assembly, args).ConfigureAwait(false);
        }
        catch (SlavPackFormatException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return InvalidContainer;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return HostFailure;
        }
    }
}
