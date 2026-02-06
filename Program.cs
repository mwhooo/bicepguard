using DriftGuard.CLI;
using System.CommandLine;

namespace DriftGuard;

/// <summary>
/// Entry point for the DriftGuard application.
/// 
/// Architecture:
/// - Command-line options are defined in CLI/CommandLineOptions.cs
/// - Input validation logic is in CLI/InputValidator.cs  
/// - Console output formatting is in CLI/ConsoleOutput.cs
/// - Main command handler logic is in CLI/DriftGuardCommand.cs
/// 
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
