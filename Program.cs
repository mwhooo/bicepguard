using BicepGuard.CLI;
using System.CommandLine;

namespace BicepGuard;

/// <summary>
/// Entry point for the BicepGuard application.
/// This keeps Program.cs focused on being a clean entry point while all the
/// command-line argument definitions, validation, and handling logic is properly
/// organized in separate, testable components.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // Instantiate the command handler and build the root command
        var command = new BicepGuardCommand();
        var rootCommand = command.BuildRootCommand();
        return await rootCommand.InvokeAsync(args);
    }
}
