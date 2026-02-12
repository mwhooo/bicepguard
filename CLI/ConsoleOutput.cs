using DriftGuard.Models;
using System.Reflection;

namespace DriftGuard.CLI;

/// <summary>
/// Handles console output with support for both Unicode and ASCII modes.
/// All methods are static as they format and output text without maintaining state.
/// </summary>
public static class ConsoleOutput
{
    /// <summary>
    /// Writes an error message to the console.
    /// </summary>
    public static void WriteError(string message, bool simpleOutput)
    {
        Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Error: {message}");
    }

    /// <summary>
    /// Writes a success message to the console.
    /// </summary>
    public static void WriteSuccess(string message, bool simpleOutput)
    {
        Console.WriteLine($"{(simpleOutput ? "[OK]" : "✅")} {message}");
    }

    /// <summary>
    /// Writes an info message to the console.
    /// </summary>
    public static void WriteInfo(string message, bool simpleOutput)
    {
        Console.WriteLine($"{(simpleOutput ? "[INFO]" : "🔍")} {message}");
    }

    /// <summary>
    /// Writes a tip/suggestion message to the console.
    /// </summary>
    public static void WriteTip(string message, bool simpleOutput)
    {
        Console.WriteLine($"{(simpleOutput ? "[TIP]" : "💡")} {message}");
    }

    /// <summary>
    /// Writes a warning message to the console.
    /// </summary>
    public static void WriteWarning(string message, bool simpleOutput)
    {
        Console.WriteLine($"{(simpleOutput ? "[DRIFT DETECTED]" : "❌")} {message}");
    }

    /// <summary>
    /// Writes a fatal error message to the console.
    /// </summary>
    public static void WriteFatal(string message, Exception? innerException, bool simpleOutput)
    {
        Console.WriteLine($"{(simpleOutput ? "[FATAL]" : "❌")} Fatal error: {message}");
        if (innerException != null)
        {
            Console.WriteLine($"   Inner exception: {innerException.Message}");
        }
    }

    /// <summary>
    /// Writes the application configuration to the console.
    /// </summary>
    public static void WriteConfiguration(
        FileInfo bicepFile,
        FileInfo? parametersFile,
        DeploymentScope scope,
        string? resourceGroup,
        string? subscription,
        string? location,
        OutputFormat outputFormat,
        bool autofix,
        FileInfo? ignoreConfig,
        bool showFiltered,
        bool simpleOutput)
    {
        var version = typeof(ConsoleOutput).Assembly // trying to get version from the assembly info, if not found default to 0.0.0
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        Console.WriteLine($"{(simpleOutput ? "[INFO]" : "🔍")} Azure DriftGuard v{version}");
        Console.WriteLine($"{(simpleOutput ? "[FILE]" : "📄")} Bicep Template: {bicepFile.Name}");

        if (parametersFile != null)
        {
            Console.WriteLine($"{(simpleOutput ? "[PARAMS]" : "📋")} Parameters File: {parametersFile.Name}");
        }

        Console.WriteLine($"{(simpleOutput ? "[SCOPE]" : "🎯")} Deployment Scope: {scope}");

        if (scope == DeploymentScope.ResourceGroup)
        {
            Console.WriteLine($"{(simpleOutput ? "[RG]" : "🏗️")}  Resource Group: {resourceGroup}");
        }
        else
        {
            Console.WriteLine($"{(simpleOutput ? "[SUB]" : "🔑")} Subscription: {subscription}");
            Console.WriteLine($"{(simpleOutput ? "[LOC]" : "🌍")} Location: {location}");
        }

        Console.WriteLine($"{(simpleOutput ? "[OUTPUT]" : "📊")} Output Format: {outputFormat}");

        if (autofix)
        {
            Console.WriteLine($"{(simpleOutput ? "[AUTOFIX]" : "🔧")} Autofix Mode: ENABLED");
        }

        // Show ignore config path being used
        var ignoreConfigPath = ignoreConfig?.FullName ?? Path.Combine(Directory.GetCurrentDirectory(), "drift-ignore.json");
        var ignoreExists = File.Exists(ignoreConfigPath);
        Console.WriteLine($"{(simpleOutput ? "[IGNORE]" : "🔇")} Ignore Config: {Path.GetFileName(ignoreConfigPath)} {(ignoreExists ? "(found)" : "(not found)")}");

        if (showFiltered)
        {
            Console.WriteLine($"{(simpleOutput ? "[AUDIT]" : "🔍")} Show Filtered: ENABLED (audit mode)");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Writes autofix attempt message to the console.
    /// </summary>
    public static void WriteAutofixAttempt(bool simpleOutput)
    {
        Console.WriteLine($"{(simpleOutput ? "[AUTOFIX]" : "🔧")} Attempting to fix drift by deploying template...");
    }

    /// <summary>
    /// Writes autofix success message to the console.
    /// </summary>
    public static void WriteAutofixSuccess(string deploymentName, bool simpleOutput)
    {
        Console.WriteLine($"{(simpleOutput ? "[FIXED]" : "✅")} Drift has been automatically fixed!");
        Console.WriteLine($"{(simpleOutput ? "[DEPLOYMENT]" : "📦")} Deployment Name: {deploymentName}");
    }

    /// <summary>
    /// Writes autofix failure message to the console.
    /// </summary>
    public static void WriteAutofixFailure(string errorMessage, bool simpleOutput)
    {
        Console.WriteLine($"{(simpleOutput ? "[AUTOFIX FAILED]" : "❌")} Failed to fix drift: {errorMessage}");
    }
}
