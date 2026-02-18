using System.Diagnostics;
using BicepGuard.Models;

namespace BicepGuard.Services;

public class AzureCliService {

    public async Task<DeploymentResult> DeployBicepTemplateAsync(
        string bicepFilePath,
        string? parametersFilePath,
        DeploymentScope scope,
        string? resourceGroup,
        string? subscription,
        string? location) {

        var deploymentName = $"drift-autofix-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        
        try {
            string arguments;
            
            // Check if this is a bicepparam file or regular bicep file
            if (Path.GetExtension(bicepFilePath).ToLowerInvariant() == ".bicepparam") {
                // For bicepparam files, we need to get the referenced bicep file and use parameters
                var referencedBicepFile = await BicepParamHelper.GetReferencedBicepFileAsync(bicepFilePath);
                arguments = BuildDeployArguments(scope, referencedBicepFile, bicepFilePath, deploymentName, resourceGroup, subscription, location);
            }
            else if (!string.IsNullOrEmpty(parametersFilePath)){
                // Using separate parameters file (JSON)
                arguments = BuildDeployArguments(scope, bicepFilePath, parametersFilePath, deploymentName, resourceGroup, subscription, location);
            }
            else {
                // Regular bicep file without parameters
                arguments = BuildDeployArguments(scope, bicepFilePath, null, deploymentName, resourceGroup, subscription, location);
            }
            
            using var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = AzureCliPathResolver.GetAzureCLIPath(),
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    EnvironmentVariables = { ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "" }
                }
            };

            process.Start();
            
            // Read both stdout and stderr to prevent buffer deadlock
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode == 0){
                return new DeploymentResult {
                    Success = true,
                    DeploymentName = deploymentName
                };
            }
            else{
                return new DeploymentResult {
                    Success = false,
                    DeploymentName = deploymentName,
                    ErrorMessage = error
                };
            }
        }
        catch (Exception ex) {
            return new DeploymentResult {
                Success = false,
                DeploymentName = deploymentName,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string BuildDeployArguments(
        DeploymentScope scope,
        string templateFile,
        string? parametersFile,
        string deploymentName,
        string? resourceGroup,
        string? subscription,
        string? location)
    {
        var parametersArg = !string.IsNullOrEmpty(parametersFile) 
            ? $" --parameters \"{parametersFile}\"" 
            : "";

        if (scope == DeploymentScope.Subscription)
        {
            // Defensive check: subscription-scope deployments require a location
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Location is required for subscription-scope deployments.", nameof(location));
            }

            // Subscription-scope deployment: az deployment sub create
            var subscriptionArg = !string.IsNullOrEmpty(subscription) 
                ? $" --subscription \"{subscription}\"" 
                : "";
            
            return $"deployment sub create --location \"{location}\"{subscriptionArg} --template-file \"{templateFile}\"{parametersArg} --name \"{deploymentName}\" --output json";
        }
        else
        {
            // Defensive check: resource-group scope deployments require a resource group
            if (string.IsNullOrWhiteSpace(resourceGroup))
            {
                throw new ArgumentException("Resource group is required for resource-group scope deployments.", nameof(resourceGroup));
            }

            // Resource-group scope deployment: az deployment group create
            return $"deployment group create --resource-group \"{resourceGroup}\" --template-file \"{templateFile}\"{parametersArg} --name \"{deploymentName}\" --output json";
        }
    }
}