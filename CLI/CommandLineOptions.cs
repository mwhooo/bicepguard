using System.CommandLine;
using BicepGuard.Models;

namespace BicepGuard.CLI;

/// <summary>
/// Defines all command-line options for BicepGuard.
/// static helpers methods
/// </summary>
public static class CommandLineOptions
{
    // AI assisted me in setting up this class, but for me it does not feel DRY enough, creating a new signatures for this overload.
    // i think 2 overloads would do instead of 10 signatures
    public static Option<FileInfo> CreateFileOptionFlexible(string opt, string desc, bool required) {
        var option = new Option<FileInfo>(opt) {
            Description = desc,
            Required = required
         };
         return option;
    }

    public static Option<FileInfo?> CreateFileOptionFlexible(string opt, string desc, bool required, string alias) {
        var option = new Option<FileInfo?>(opt, alias) {
            Description = desc,
            Required = required
         };
         return option;
    }

    public static Option<string?> CreateStringOptionFlexible(string opt, string desc, bool required) {
        var option = new Option<string?>(opt) {
            Description = desc,
            Required = required
         };
         return option;
    }


    /// <summary>
    /// Creates the deployment scope option.
    /// Defaults to ResourceGroup scope.
    /// </summary>
    public static Option<DeploymentScope> CreateScopeOption()
    {
        var option = new Option<DeploymentScope>("--scope")
        {
            Description = "Deployment scope: ResourceGroup (default) or Subscription",
            Required = false,
            DefaultValueFactory = (_) => DeploymentScope.ResourceGroup
        };
        return option;
    }

    /// <summary>
    /// Creates the output format option.
    /// </summary>
    public static Option<OutputFormat> CreateOutputFormatOption()
    {
        var option = new Option<OutputFormat>("--output")
        {
            Description = "Output format",
            Required = false,
            DefaultValueFactory = (_) => OutputFormat.Console
        };
        return option;
    }

    /// <summary>
    /// Creates the simple output option for better terminal compatibility.
    /// Just a simple bool switch - if specified we use ASCII output.
    /// </summary>
    public static Option<bool> CreateSimpleOutputOption()
    {
        var option = new Option<bool>("--simple-output")
        {
            Description = "Use simple ASCII characters instead of Unicode symbols",
            Required = false,
            DefaultValueFactory = (_) => false
        };
        return option;
    }

    /// <summary>
    /// Creates the ignore configuration file option.
    /// You can name the file whatever you want but it will default to drift-ignore.json
    /// in the current directory if not specified.
    /// </summary>
    public static Option<FileInfo?> CreateIgnoreConfigOption()
    {
        var option = new Option<FileInfo?>("--ignore-config")
        {
            Description = "Path to drift ignore configuration file (default: drift-ignore.json)",
            Required = false
        };
        return option;
    }

    /// <summary>
    /// Creates the show filtered option for transparency/auditing.
    /// This allows users to see the details of the drifts that were filtered out
    /// based on the ignore configuration.
    /// </summary>
    public static Option<bool> CreateShowFilteredOption()
    {
        var option = new Option<bool>("--show-filtered")
        {
            Description = "Show details of filtered/ignored drifts for auditing purposes",
            Required = false,
            DefaultValueFactory = (_) => false
        };
        return option;
    }
}
