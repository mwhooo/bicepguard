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
        var option = new Option<FileInfo>(
            name: "--bicep-file",
            description: "Path to the Bicep template file (.bicep) or parameters file (.bicepparam)")
        {
            IsRequired = true
        };
        return option;
    }

    /// <summary>
    /// Creates the parameters file option for JSON parameter files.
    /// Supports both --parameters-file and -p as a shorthand.
    /// </summary>
    public static Option<FileInfo?> CreateParametersFileOption()
    {
        var option = new Option<FileInfo?>(
            aliases: new[] { "--parameters-file", "-p" },
            description: "Path to ARM JSON parameters file (.json) to use with the Bicep template")
        {
            IsRequired = false
        };
        return option;
    }

    /// <summary>
    /// Creates the deployment scope option.
    /// Defaults to ResourceGroup scope.
    /// </summary>
    public static Option<DeploymentScope> CreateScopeOption()
    {
        var option = new Option<DeploymentScope>(
            name: "--scope",
            description: "Deployment scope: ResourceGroup (default) or Subscription")
        {
            IsRequired = false
        };
        option.SetDefaultValue(DeploymentScope.ResourceGroup);
        return option;
    }

    /// <summary>
    /// Creates the resource group option (required for ResourceGroup scope).
    /// </summary>
    public static Option<string?> CreateResourceGroupOption()
    {
        var option = new Option<string?>(
            name: "--resource-group",
            description: "Azure resource group name (required for ResourceGroup scope)")
        {
            IsRequired = false
        };
        return option;
    }

    /// <summary>
    /// Creates the subscription option (required for Subscription scope).
    /// </summary>
    public static Option<string?> CreateSubscriptionOption()
    {
        var option = new Option<string?>(
            name: "--subscription",
            description: "Azure subscription ID (required for Subscription scope)")
        {
            IsRequired = false
        };
        return option;
    }

    /// <summary>
    /// Creates the location option (required for Subscription scope deployments).
    /// </summary>
    public static Option<string?> CreateLocationOption()
    {
        var option = new Option<string?>(
            name: "--location",
            description: "Azure region for deployment (required for Subscription scope)")
        {
            IsRequired = false
        };
        return option;
    }

    /// <summary>
    /// Creates the output format option.
    /// </summary>
    public static Option<OutputFormat> CreateOutputFormatOption()
    {
        var option = new Option<OutputFormat>(
            name: "--output",
            description: "Output format")
        {
            IsRequired = false
        };
        option.SetDefaultValue(OutputFormat.Console);
        return option;
    }

    /// <summary>
    /// Creates the simple output option for better terminal compatibility.
    /// Just a simple bool switch - if specified we use ASCII output.
    /// </summary>
    public static Option<bool> CreateSimpleOutputOption()
    {
        var option = new Option<bool>(
            name: "--simple-output",
            description: "Use simple ASCII characters instead of Unicode symbols")
        {
            IsRequired = false
        };
        option.SetDefaultValue(false);
        return option;
    }

    /// <summary>
    /// Creates the autofix option to deploy template when drift is detected.
    /// Note: Consider using CI/CD or GitOps approach instead for production.
    /// </summary>
    public static Option<bool> CreateAutofixOption()
    {
        var option = new Option<bool>(
            name: "--autofix",
            description: "Automatically deploy the Bicep template to fix detected drift")
        {
            IsRequired = false
        };
        option.SetDefaultValue(false);
        return option;
    }

    /// <summary>
    /// Creates the ignore configuration file option.
    /// You can name the file whatever you want but it will default to drift-ignore.json
    /// in the current directory if not specified.
    /// </summary>
    public static Option<FileInfo?> CreateIgnoreConfigOption()
    {
        var option = new Option<FileInfo?>(
            name: "--ignore-config",
            description: "Path to drift ignore configuration file (default: drift-ignore.json)")
        {
            IsRequired = false
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
        var option = new Option<bool>(
            name: "--show-filtered",
            description: "Show details of filtered/ignored drifts for auditing purposes")
        {
            IsRequired = false
        };
        option.SetDefaultValue(false);
        return option;
    }
}
