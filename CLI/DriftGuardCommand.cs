using System.CommandLine;
using BicepGuard.Core;
using BicepGuard.Models;

namespace BicepGuard.CLI;

/// <summary>
/// Handles the main command execution for BicepGuard.
/// </summary>
public class BicepGuardCommand
{
    // Private option fields
    private Option<FileInfo>? _bicepFileOption;
    private Option<FileInfo?>? _parametersFileOption;
    private Option<DeploymentScope>? _scopeOption;
    private Option<string?>? _resourceGroupOption;
    private Option<string?>? _subscriptionOption;
    private Option<string?>? _locationOption;
    private Option<OutputFormat>? _outputFormatOption;
    private Option<bool>? _simpleOutputOption;
    private Option<FileInfo?>? _ignoreConfigOption;
    private Option<bool>? _showFilteredOption;

    // Public properties for parsed values
    public FileInfo? BicepFile { get; set; }
    public FileInfo? ParametersFile { get; set; }
    public DeploymentScope Scope { get; set; }
    public string? ResourceGroup { get; set; }
    public string? Subscription { get; set; }
    public string? Location { get; set; }
    public OutputFormat OutputFormat { get; set; }
    public bool SimpleOutput { get; set; }
    public FileInfo? IgnoreConfig { get; set; }
    public bool ShowFiltered { get; set; }

    /// <summary>
    /// Builds the root command with all options configured.
    /// </summary>
    public RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("Azure BicepGuard - Configuration Drift Detector")
        {
            Description = "Detects configuration drift between Bicep/ARM templates and live Azure resources"
        };

        _bicepFileOption = CommandLineOptions.CreateFileOptionFlexible("--bicep-file",
            "Path to the Bicep template file (.bicep) or parameters file (.bicepparam)", true);

        _parametersFileOption = CommandLineOptions.CreateFileOptionFlexible("--parameters-file",
            "Path to ARM JSON parameters file (.json) to use with the Bicep template", false, "-p");

        _resourceGroupOption = CommandLineOptions.CreateStringOptionFlexible("--resource-group",
            "Azure resource group name (required for ResourceGroup scope)", false);

        _subscriptionOption = CommandLineOptions.CreateStringOptionFlexible("--subscription",
            "Azure subscription ID (required for Subscription scope)", false);

        _locationOption = CommandLineOptions.CreateStringOptionFlexible("--location",
            "Azure region/location (required for Subscription scope)", false);

        _ignoreConfigOption = CommandLineOptions.CreateFileOptionFlexible("--ignore-config",
            "Path to a JSON file specifying which drift types or specific resources to ignore", false, "-l");

        _scopeOption = CommandLineOptions.CreateScopeOption();
        _outputFormatOption = CommandLineOptions.CreateOutputFormatOption();
        _simpleOutputOption = CommandLineOptions.CreateSimpleOutputOption();
        _showFilteredOption = CommandLineOptions.CreateShowFilteredOption();

        rootCommand.Add(_bicepFileOption);
        rootCommand.Add(_parametersFileOption);
        rootCommand.Add(_scopeOption);
        rootCommand.Add(_resourceGroupOption);
        rootCommand.Add(_subscriptionOption);
        rootCommand.Add(_locationOption);
        rootCommand.Add(_outputFormatOption);
        rootCommand.Add(_simpleOutputOption);
        rootCommand.Add(_ignoreConfigOption);
        rootCommand.Add(_showFilteredOption);

        // Set the handler - captures 'this' to access instance methods and properties
        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            await HandleCommandAsync(parseResult);
        });

        return rootCommand;
    }

    /// <summary>
    /// Handles the command execution logic.
    /// </summary>
    private async Task HandleCommandAsync(ParseResult parseResult)
    {
        // Extract all option values into properties
        BicepFile = parseResult.GetValue(_bicepFileOption!)!;
        ParametersFile = parseResult.GetValue(_parametersFileOption!);
        Scope = parseResult.GetValue(_scopeOption!);
        ResourceGroup = parseResult.GetValue(_resourceGroupOption!);
        Subscription = parseResult.GetValue(_subscriptionOption!);
        Location = parseResult.GetValue(_locationOption!);
        OutputFormat = parseResult.GetValue(_outputFormatOption!);
        SimpleOutput = parseResult.GetValue(_simpleOutputOption!);
        // Autofix = parseResult.GetValue(_autofixOption!);
        IgnoreConfig = parseResult.GetValue(_ignoreConfigOption!);
        ShowFiltered = parseResult.GetValue(_showFilteredOption!);

        try
        {
            // Set up console encoding and simple output mode
            Console.OutputEncoding = System.Text.Encoding.UTF8; // this supports emojis in the console output
            Environment.SetEnvironmentVariable("SIMPLE_OUTPUT", SimpleOutput.ToString()); // set environment variable to indicate simple output mode for use in ConsoleOutput
            Environment.SetEnvironmentVariable("SHOW_FILTERED", ShowFiltered.ToString()); // set environment variable to indicate whether to show filtered results

            // Validate all inputs
            if (!ValidateInputs())
            {
                Environment.Exit(1);
                return;
            }

            // Display configuration
            ConsoleOutput.WriteConfiguration(
                BicepFile,
                ParametersFile,
                Scope,
                ResourceGroup,
                Subscription,
                Location,
                OutputFormat,
                IgnoreConfig,
                ShowFiltered,
                SimpleOutput);

            // Execute drift detection
            var detector = new DriftDetector(IgnoreConfig?.FullName);
            var result = await detector.DetectDriftAsync(
                BicepFile,
                ParametersFile,
                Scope,
                ResourceGroup,
                Subscription,
                Location,
                OutputFormat);

            // Handle results
            HandleDriftResultAsync(result, detector);
        }
        catch (InvalidOperationException)
        {
            // Validation errors already have detailed output from BicepService
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteFatal(ex.Message, ex.InnerException, SimpleOutput);
            ConsoleOutput.WriteTip("Ensure Azure CLI is installed and you're logged in with 'az login'", SimpleOutput);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Validates all input arguments.
    /// </summary>
    private bool ValidateInputs()
    {
        // Validate Bicep file
        if (!BicepFile!.Exists) {
            ConsoleOutput.WriteError($"Bicep file not found: {BicepFile.FullName}", SimpleOutput);
            return false;
        }

        // Validate parameter configuration
        var paramConfigValidation = InputValidator.ValidateBicepParamsSpecified(BicepFile, ParametersFile);
        if (!paramConfigValidation.IsValid){
            ConsoleOutput.WriteError(paramConfigValidation.ErrorMessage!, SimpleOutput);
            return false;
        }

        // Validate parameters file if provided
        var paramFileValidation = InputValidator.ValidateParametersFile(ParametersFile);
        if (!paramFileValidation.IsValid)
        {
            ConsoleOutput.WriteError(paramFileValidation.ErrorMessage!, SimpleOutput);
            return false;
        }

        // Validate scope-specific requirements
        if (Scope == DeploymentScope.ResourceGroup)
        {
            var rgValidation = InputValidator.ValidateResourceGroupScope(ResourceGroup);
            if (!rgValidation.IsValid)
            {
                ConsoleOutput.WriteError(rgValidation.ErrorMessage!, SimpleOutput);
                return false;
            }
        }
        else if (Scope == DeploymentScope.Subscription)
        {
            var subValidation = InputValidator.ValidateSubscriptionScope(Subscription, Location);
            if (!subValidation.IsValid)
            {
                ConsoleOutput.WriteError(subValidation.ErrorMessage!, SimpleOutput);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Handles the drift detection result and autofix if requested.
    /// </summary>
    private void HandleDriftResultAsync(DriftDetectionResult result, DriftDetector detector)
    {
        if (result.HasDrift)
        {
            ConsoleOutput.WriteWarning("Configuration drift detected!", SimpleOutput);

            //if (Autofix)
            //{
            //    ConsoleOutput.WriteAutofixAttempt(SimpleOutput);
            //    var deploymentResult = await detector.DeployTemplateAsync(
            //        BicepFile!,
            //        ParametersFile,
            //        Scope,
            //        ResourceGroup,
            //        Subscription,
            //        Location);

            //    if (deploymentResult.Success)
            //    {
            //        ConsoleOutput.WriteAutofixSuccess(deploymentResult.DeploymentName, SimpleOutput);
            //        Environment.Exit(0);
            //    }
            //    else
            //    {
            //        ConsoleOutput.WriteAutofixFailure(deploymentResult.ErrorMessage ?? "Unknown error", SimpleOutput);
            //        Environment.Exit(1);
            //    }
            //}
            //else
            //{
            //    ConsoleOutput.WriteTip("Use --autofix to automatically deploy template and fix drift.", SimpleOutput);
            //    Environment.Exit(1);
            //}
        }
        else
        {
            ConsoleOutput.WriteSuccess("No configuration drift detected.", SimpleOutput);
            Environment.Exit(0);
        }
    }
}
