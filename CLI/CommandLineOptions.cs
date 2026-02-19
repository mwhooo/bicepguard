using System.CommandLine;
using BicepGuard.Models;

namespace BicepGuard.CLI;

/// <summary>
/// Defines all command-line options for BicepGuard.
/// static helpers methods
/// </summary>
public static class CommandLineOptions
{
    /// <summary>
    /// Creates the Bicep file option (accepts both .bicep and .bicepparam files).
    /// Bicepparam has a using statement on top which refers to the bicep file,
    /// so we don't need to specify the bicep file separately if we use a bicepparam file.
    /// </summary>
    public static Option<FileInfo> CreateBicepFileOption()
    {
        var option = new Option<FileInfo>("--bicep-file")
        {
            Description = "Path to the Bicep template file (.bicep) or parameters file (.bicepparam)",
            Required = true
        };
        return option;
    }

    /// <summary>
    /// Creates the parameters file option for JSON parameter files.
    /// Supports both --parameters-file and -p as a shorthand.
    /// </summary>
    public static Option<FileInfo?> CreateParametersFileOption()
    {
        var option = new Option<FileInfo?>("--parameters-file", "-p")
        {
            Description = "Path to ARM JSON parameters file (.json) to use with the Bicep template",
            Required = false
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
    /// Creates the resource group option (required for ResourceGroup scope).
    /// </summary>
    public static Option<string?> CreateResourceGroupOption()
    {
        var option = new Option<string?>("--resource-group")
        {
            Description = "Azure resource group name (required for ResourceGroup scope)",
            Required = false
        };
        return option;
    }

    /// <summary>
    /// Creates the subscription option (required for Subscription scope).
    /// </summary>
    public static Option<string?> CreateSubscriptionOption()
    {
        var option = new Option<string?>("--subscription")
        {
            Description = "Azure subscription ID (required for Subscription scope)",
            Required = false
        };
        return option;
    }

    /// <summary>
    /// Creates the location option (required for Subscription scope deployments).
    /// </summary>
    public static Option<string?> CreateLocationOption()
    {
        var option = new Option<string?>("--location")
        {
            Description = "Azure region for deployment (required for Subscription scope)",
            Required = false
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
    /// Creates the autofix option to deploy template when drift is detected.
    /// Note: Consider using CI/CD or GitOps approach instead for production.
    /// </summary>
    public static Option<bool> CreateAutofixOption()
    {
        var option = new Option<bool>("--autofix")
        {
            Description = "Automatically deploy the Bicep template to fix detected drift",
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
