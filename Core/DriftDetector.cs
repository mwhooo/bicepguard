using DriftGuard.Models;
using DriftGuard.Services;

namespace BicepGuard.Core;

public class DriftDetector
{
    private readonly BicepService _bicepService;
    private readonly AzureCliService _azureCliService;
    private readonly ComparisonService _comparisonService;
    private readonly ReportingService _reportingService;
    private readonly DriftIgnoreService? _ignoreService;
    private readonly WhatIfJsonService _whatIfJsonService;

    public DriftDetector(string? ignoreConfigPath = null)
    {
        _bicepService = new BicepService();
        _azureCliService = new AzureCliService();
        _ignoreService = !string.IsNullOrEmpty(ignoreConfigPath) ? new DriftIgnoreService(ignoreConfigPath) : new DriftIgnoreService();
        _comparisonService = new ComparisonService(_ignoreService);
        _reportingService = new ReportingService();
        _whatIfJsonService = new WhatIfJsonService(_ignoreService);
    }

    public async Task<DriftDetectionResult> DetectDriftAsync(
        FileInfo bicepFile,
        FileInfo? parametersFile,
        DeploymentScope scope,
        string? resourceGroup,
        string? subscription,
        string? location,
        OutputFormat outputFormat = OutputFormat.Console)
    {
        var targetDescription = scope == DeploymentScope.ResourceGroup 
            ? $"resource group: {resourceGroup}" 
            : $"subscription: {subscription}";
        
        Console.WriteLine($"🔍 Starting drift detection for {targetDescription}");
        Console.WriteLine($"📄 Using Bicep template: {bicepFile.FullName}");
        if (parametersFile != null)
        {
            Console.WriteLine($"📋 Using parameters file: {parametersFile.FullName}");
        }

        try
        {
            // Use the new JSON-based what-if service for more reliable drift detection
            var result = await _whatIfJsonService.RunWhatIfAsync(
                bicepFile.FullName,
                parametersFile?.FullName,
                scope, 
                resourceGroup, 
                subscription, 
                location);

            // Generate report
            Console.WriteLine("📊 Generating drift report...");
            await _reportingService.GenerateReportAsync(result, outputFormat);

            return result;
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation errors without additional logging (already logged by WhatIfJsonService)
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during drift detection: {ex.Message}");
            throw;
        }
    }

    // Backward compatibility overload for resource-group scope
    public Task<DriftDetectionResult> DetectDriftAsync(
        FileInfo bicepFile, 
        string resourceGroup, 
        OutputFormat outputFormat = OutputFormat.Console)
    {
        return DetectDriftAsync(bicepFile, null, DeploymentScope.ResourceGroup, resourceGroup, null, null, outputFormat);
    }

    public async Task<DeploymentResult> DeployTemplateAsync(
        FileInfo bicepFile,
        FileInfo? parametersFile,
        DeploymentScope scope,
        string? resourceGroup,
        string? subscription,
        string? location)
    {
        bool simpleOutput = Environment.GetEnvironmentVariable("SIMPLE_OUTPUT") == "True";
        
        var targetDescription = scope == DeploymentScope.ResourceGroup 
            ? $"resource group: {resourceGroup}" 
            : $"subscription: {subscription}";
        
        try
        {
            Console.WriteLine($"{(simpleOutput ? "[DEPLOY]" : "🚀")} Deploying Bicep template to {targetDescription}");
            Console.WriteLine($"{(simpleOutput ? "[FILE]" : "📄")} Template file: {bicepFile.FullName}");
            if (parametersFile != null)
            {
                Console.WriteLine($"{(simpleOutput ? "[PARAMS]" : "📋")} Parameters file: {parametersFile.FullName}");
            }

            var result = await _azureCliService.DeployBicepTemplateAsync(
                bicepFile.FullName,
                parametersFile?.FullName,
                scope, 
                resourceGroup, 
                subscription, 
                location);
            
            if (result.Success)
            {
                Console.WriteLine($"{(simpleOutput ? "[SUCCESS]" : "✅")} Deployment completed successfully!");
            }
            else
            {
                Console.WriteLine($"{(simpleOutput ? "[FAILED]" : "❌")} Deployment failed!");
            }

            return result;
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Bicep file not found: {ex.Message}");
            return new DeploymentResult
            {
                Success = false,
                ErrorMessage = $"Bicep file not found: {ex.Message}"
            };
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Azure CLI error during deployment: {ex.Message}");
            return new DeploymentResult
            {
                Success = false,
                ErrorMessage = $"Azure CLI error: {ex.Message}"
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Access denied during deployment: {ex.Message}");
            return new DeploymentResult
            {
                Success = false,
                ErrorMessage = $"Access denied: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            // Catch any other unexpected exceptions to ensure we always return a structured result
            Console.WriteLine($"{(simpleOutput ? "[ERROR]" : "❌")} Unexpected error during deployment: {ex.Message}");
            return new DeploymentResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    // Backward compatibility overload for resource-group scope
    public Task<DeploymentResult> DeployTemplateAsync(FileInfo bicepFile, string resourceGroup)
    {
        return DeployTemplateAsync(bicepFile, null, DeploymentScope.ResourceGroup, resourceGroup, null, null);
    }
}