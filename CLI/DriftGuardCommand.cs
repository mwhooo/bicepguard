using DriftGuard.Core;
using DriftGuard.Models;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace DriftGuard.CLI;

/// <summary>
/// Handles the main command execution for DriftGuard.
/// </summary>
public static class DriftGuardCommand
{
    /// <summary>
    /// Builds the root command with all options configured.
    /// </summary>
    /// 
    /// 
    public static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("Azure DriftGuard - Configuration Drift Detector")
        {
            Description = "Detects configuration drift between Bicep/ARM templates and live Azure resources"
        };

        // Add all options to var, since we use them twice - once for defining the command and once for extracting values in the handler
        var bicepFileOption = CommandLineOptions.CreateBicepFileOption();
        var parametersFileOption = CommandLineOptions.CreateParametersFileOption();
        var scopeOption = CommandLineOptions.CreateScopeOption();
        var resourceGroupOption = CommandLineOptions.CreateResourceGroupOption();
        var subscriptionOption = CommandLineOptions.CreateSubscriptionOption();
        var locationOption = CommandLineOptions.CreateLocationOption();
        var outputFormatOption = CommandLineOptions.CreateOutputFormatOption();
        var simpleOutputOption = CommandLineOptions.CreateSimpleOutputOption();
        var autofixOption = CommandLineOptions.CreateAutofixOption();
        var ignoreConfigOption = CommandLineOptions.CreateIgnoreConfigOption();
        var showFilteredOption = CommandLineOptions.CreateShowFilteredOption();

        rootCommand.Add(bicepFileOption);
        rootCommand.Add(parametersFileOption);
        rootCommand.Add(scopeOption);
        rootCommand.Add(resourceGroupOption);
        rootCommand.Add(subscriptionOption);
        rootCommand.Add(locationOption);
        rootCommand.Add(outputFormatOption);
        rootCommand.Add(simpleOutputOption);
        rootCommand.Add(autofixOption);
        rootCommand.Add(ignoreConfigOption);
        rootCommand.Add(showFilteredOption);

        // Set the handler
        rootCommand.SetHandler(async (context) =>
        {
            await HandleCommandAsync(
                context,
                bicepFileOption,
                parametersFileOption,
                scopeOption,
                resourceGroupOption,
                subscriptionOption,
                locationOption,
                outputFormatOption,
                simpleOutputOption,
                autofixOption,
                ignoreConfigOption,
                showFilteredOption);
        });

        return rootCommand;
    }

    /// <summary>
    /// Handles the command execution logic.
    /// </summary>
    /// 
    /// i let copilot do the scaffolding, again its not bad, but why not pass an object around, i see
    /// method signatures with like zillion input params. this can be handled quite differently, probably saving alot of codespace
    private static async Task HandleCommandAsync(
        InvocationContext context,
        Option<FileInfo> bicepFileOption,
        Option<FileInfo?> parametersFileOption,
        Option<DeploymentScope> scopeOption,
        Option<string?> resourceGroupOption,
        Option<string?> subscriptionOption,
        Option<string?> locationOption,
        Option<OutputFormat> outputFormatOption,
        Option<bool> simpleOutputOption,
        Option<bool> autofixOption,
        Option<FileInfo?> ignoreConfigOption,
        Option<bool> showFilteredOption)
    {
        // Extract all option values
        var bicepFile = context.ParseResult.GetValueForOption(bicepFileOption)!;
        var parametersFile = context.ParseResult.GetValueForOption(parametersFileOption);
        var scope = context.ParseResult.GetValueForOption(scopeOption);
        var resourceGroup = context.ParseResult.GetValueForOption(resourceGroupOption);
        var subscription = context.ParseResult.GetValueForOption(subscriptionOption);
        var location = context.ParseResult.GetValueForOption(locationOption);
        var outputFormat = context.ParseResult.GetValueForOption(outputFormatOption);
        var simpleOutput = context.ParseResult.GetValueForOption(simpleOutputOption);
        var autofix = context.ParseResult.GetValueForOption(autofixOption);
        var ignoreConfig = context.ParseResult.GetValueForOption(ignoreConfigOption);
        var showFiltered = context.ParseResult.GetValueForOption(showFilteredOption);

        try
        {
            // Set up console encoding and simple output mode
            Console.OutputEncoding = System.Text.Encoding.UTF8; // this supports emojis in the console output
            Environment.SetEnvironmentVariable("SIMPLE_OUTPUT", simpleOutput.ToString()); // set environment variable to indicate simple output mode for use in ConsoleOutput
            Environment.SetEnvironmentVariable("SHOW_FILTERED", showFiltered.ToString()); // set environment variable to indicate whether to show filtered results

            // Validate all inputs
            if (!ValidateInputs(bicepFile, parametersFile, scope, resourceGroup, subscription, location, simpleOutput))
            {
                Environment.Exit(1);
                return;
            }

            // Display configuration
            ConsoleOutput.WriteConfiguration(
                bicepFile,
                parametersFile,
                scope,
                resourceGroup,
                subscription,
                location,
                outputFormat,
                autofix,
                ignoreConfig,
                showFiltered,
                simpleOutput);

            // Execute drift detection
            var detector = new DriftDetector(ignoreConfig?.FullName);
            var result = await detector.DetectDriftAsync(
                bicepFile,
                parametersFile,
                scope,
                resourceGroup,
                subscription,
                location,
                outputFormat);

            // Handle results
            await HandleDriftResultAsync(
                result,
                detector,
                bicepFile,
                parametersFile,
                scope,
                resourceGroup,
                subscription,
                location,
                autofix,
                simpleOutput);
        }
        catch (InvalidOperationException)
        {
            // Validation errors already have detailed output from BicepService
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteFatal(ex.Message, ex.InnerException, simpleOutput);
            ConsoleOutput.WriteTip("Ensure Azure CLI is installed and you're logged in with 'az login'", simpleOutput);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Validates all input arguments.
    /// 
    /// 
    /// </summary>
    
    // i let copilot scaffold, its not bad, but i see repetative stuff, which i fixed already a couple
    // i would have handled the boolean return differently for instance, 
    // not really best practice to return in an if statement, i might revisit this later
    private static bool ValidateInputs(
        FileInfo bicepFile,
        FileInfo? parametersFile,
        DeploymentScope scope,
        string? resourceGroup,
        string? subscription,
        string? location,
        bool simpleOutput)
    {
        // Validate Bicep file
        if (!bicepFile.Exists) {
            ConsoleOutput.WriteError($"Bicep file not found: {bicepFile.FullName}", simpleOutput);
            return false;
        }

        // Validate parameter configuration
        var paramConfigValidation = InputValidator.ValidateBicepParamsSpecified(bicepFile, parametersFile);
        if (!paramConfigValidation.IsValid){
            ConsoleOutput.WriteError(paramConfigValidation.ErrorMessage!, simpleOutput);
            return false;
        }

        // Validate parameters file if provided
        var paramFileValidation = InputValidator.ValidateParametersFile(parametersFile);
        if (!paramFileValidation.IsValid)
        {
            ConsoleOutput.WriteError(paramFileValidation.ErrorMessage!, simpleOutput);
            return false;
        }

        // Validate scope-specific requirements
        if (scope == DeploymentScope.ResourceGroup)
        {
            var rgValidation = InputValidator.ValidateResourceGroupScope(resourceGroup);
            if (!rgValidation.IsValid)
            {
                ConsoleOutput.WriteError(rgValidation.ErrorMessage!, simpleOutput);
                return false;
            }
        }
        else if (scope == DeploymentScope.Subscription)
        {
            var subValidation = InputValidator.ValidateSubscriptionScope(subscription, location);
            if (!subValidation.IsValid)
            {
                ConsoleOutput.WriteError(subValidation.ErrorMessage!, simpleOutput);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Handles the drift detection result and autofix if requested.
    /// </summary>
    private static async Task HandleDriftResultAsync(
        DriftDetectionResult result,
        DriftDetector detector,
        FileInfo bicepFile,
        FileInfo? parametersFile,
        DeploymentScope scope,
        string? resourceGroup,
        string? subscription,
        string? location,
        bool autofix,
        bool simpleOutput)
    {
        if (result.HasDrift)
        {
            ConsoleOutput.WriteWarning("Configuration drift detected!", simpleOutput);

            if (autofix)
            {
                ConsoleOutput.WriteAutofixAttempt(simpleOutput);
                var deploymentResult = await detector.DeployTemplateAsync(
                    bicepFile,
                    parametersFile,
                    scope,
                    resourceGroup,
                    subscription,
                    location);

                if (deploymentResult.Success)
                {
                    ConsoleOutput.WriteAutofixSuccess(deploymentResult.DeploymentName, simpleOutput);
                    Environment.Exit(0);
                }
                else
                {
                    ConsoleOutput.WriteAutofixFailure(deploymentResult.ErrorMessage ?? "Unknown error", simpleOutput);
                    Environment.Exit(1);
                }
            }
            else
            {
                ConsoleOutput.WriteTip("Use --autofix to automatically deploy template and fix drift.", simpleOutput);
                Environment.Exit(1);
            }
        }
        else
        {
            ConsoleOutput.WriteSuccess("No configuration drift detected.", simpleOutput);
            Environment.Exit(0);
        }
    }
}
