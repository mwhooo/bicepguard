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
        // Build the root command with all options and handlers configured
        // See CLI/DriftGuardCommand.BuildRootCommand() for full command setup
        var rootCommand = DriftGuardCommand.BuildRootCommand();
        return await rootCommand.InvokeAsync(args);
    }
}
