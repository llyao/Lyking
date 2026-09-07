using Lyking.AiWorkflow.Commands;

namespace Lyking.AiWorkflow;

internal static class Program
{
    public static Task<int> Main(string[] args) => CommandDispatcher.RunAsync(args, Directory.GetCurrentDirectory(), Console.Out, Console.Error);
}
