using DriftGuard.CLI;
using System.CommandLine;

namespace DriftGuard;

/// <summary>
/// Entry point for the DriftGuard application.
/// This keeps Program.cs focused on being a clean entry point while all the
/// command-line argument definitions, validation, and handling logic is properly
/// organized in separate, testable components.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // Instantiate the command handler and build the root command
        var command = new DriftGuardCommand();
        var rootCommand = command.BuildRootCommand();
        return await rootCommand.InvokeAsync(args);
    }
}
