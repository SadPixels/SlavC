using System.Reflection;
using System.Runtime.ExceptionServices;

namespace RusLang.RuntimeHost;

public static class EntryPointInvoker
{
    public static async Task<int> InvokeAsync(Assembly assembly, string[] args)
    {
        var entryPoint = assembly.EntryPoint ??
            throw new InvalidOperationException("RUSH1004: Entry assembly has no entry point.");
        var parameters = entryPoint.GetParameters();
        object?[]? invocationArguments = parameters.Length switch
        {
            0 => null,
            1 when parameters[0].ParameterType == typeof(string[]) => [args],
            _ => throw new InvalidOperationException(
                $"RUSH1005: Unsupported entry point signature '{entryPoint}'."),
        };

        object? result;
        try
        {
            result = entryPoint.Invoke(null, invocationArguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        return result switch
        {
            null => 0,
            int exitCode => exitCode,
            Task<int> exitCodeTask => await exitCodeTask.ConfigureAwait(false),
            Task task => await AwaitVoidTask(task).ConfigureAwait(false),
            _ => throw new InvalidOperationException(
                $"RUSH1006: Unsupported entry point return type '{entryPoint.ReturnType}'."),
        };
    }

    private static async Task<int> AwaitVoidTask(Task task)
    {
        await task.ConfigureAwait(false);
        return 0;
    }
}
