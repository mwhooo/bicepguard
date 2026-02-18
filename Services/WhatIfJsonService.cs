using System.Diagnostics;
using System.Text.Json;
using BicepGuard.Models;

namespace BicepGuard.Services;

/// <summary>
/// Service to parse what-if JSON output for drift detection.
/// Uses --no-pretty-print to get structured JSON instead of text parsing.
/// Supports both resource-group and subscription scope deployments.
/// </summary>
public class WhatIfJsonService
{
    private readonly DriftIgnoreService? _ignoreService;

    public WhatIfJsonService(DriftIgnoreService? ignoreService = null)
    {
        _ignoreService = ignoreService;
    }

    /// <summary>
    /// Runs az deployment what-if with JSON output and parses the results.
    /// Supports both resource-group and subscription scope deployments.
    /// </summary>
    public async Task<DriftDetectionResult> RunWhatIfAsync(
        string bicepFilePath,
        string? parametersFilePath,
        DeploymentScope scope,
        string? resourceGroup,
        string? subscription,
        string? location)
    {
        try
        {
            var fileExtension = Path.GetExtension(bicepFilePath).ToLowerInvariant();
            string argumentsString;
            string templateFile;

            if (fileExtension == ".bicepparam")
            {
                var referencedBicepFile = await GetReferencedBicepFileAsync(bicepFilePath);
                templateFile = referencedBicepFile;
                argumentsString = BuildWhatIfArguments(scope, referencedBicepFile, bicepFilePath, resourceGroup, subscription, location);
            }
            else if (!string.IsNullOrEmpty(parametersFilePath))
            {
                // Using separate parameters file (JSON)
                templateFile = bicepFilePath;
                argumentsString = BuildWhatIfArguments(scope, bicepFilePath, parametersFilePath, resourceGroup, subscription, location);
            }
            else
            {
                templateFile = bicepFilePath;
                argumentsString = BuildWhatIfArguments(scope, bicepFilePath, null, resourceGroup, subscription, location);
            }

            var scopeDescription = scope == DeploymentScope.ResourceGroup 
                ? $"resource group '{resourceGroup}'" 
                : $"subscription '{subscription}'";
            
            Console.WriteLine($"📋 Running what-if analysis (JSON mode)...");
            Console.WriteLine($"   Template: {Path.GetFileName(templateFile)}");
            Console.WriteLine($"   Scope: {scopeDescription}");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = AzureCliPathResolver.GetAzureCLIPath(),
                    Arguments = argumentsString,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"❌ What-if command failed");
                Console.WriteLine($"   Error: {error}");
                throw new InvalidOperationException($"What-if failed: {error}");
            }

            Console.WriteLine($"✅ What-if analysis completed successfully");
            
            // Strip any non-JSON prefix (Azure CLI sometimes outputs messages before JSON)
            var jsonOutput = ExtractJson(output);
            
            // Parse the JSON output
            var result = ParseWhatIfJson(jsonOutput);
            
            // Apply ignore filters if configured
            if (_ignoreService != null)
            {
                result = _ignoreService.FilterIgnoredDrifts(result);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error running what-if: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Backward compatibility overload for resource-group scope.
    /// </summary>
    public Task<DriftDetectionResult> RunWhatIfAsync(string bicepFilePath, string resourceGroup)
    {
        return RunWhatIfAsync(bicepFilePath, null, DeploymentScope.ResourceGroup, resourceGroup, null, null);
    }

    /// <summary>
    /// Extracts JSON from output that may contain non-JSON prefix text.
    /// Azure CLI sometimes outputs messages like "Bicep CLI is already installed..." before JSON.
    /// Uses validation to ensure we find actual valid JSON, not just a '{' or '[' in warning text.
    /// </summary>
    private static string ExtractJson(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return output;
        }

        // Fast path: if the entire output is valid JSON, just return it
        if (TryParseJson(output))
        {
            return output;
        }

        // Search for a valid JSON substring starting at each '{' or '['
        for (var i = 0; i < output.Length; i++)
        {
            var ch = output[i];
            if (ch != '{' && ch != '[')
            {
                continue;
            }

            var candidate = output.Substring(i);
            if (TryParseJson(candidate))
            {
                return candidate;
            }
        }

        // Fall back to returning the original output if no valid JSON found
        return output;
    }

    /// <summary>
    /// Attempts to parse a string as JSON to validate it.
    /// </summary>
    private static bool TryParseJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildWhatIfArguments(
        DeploymentScope scope,
        string templateFile,
        string? parametersFile,
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

            // Subscription-scope deployment: az deployment sub what-if
            var subscriptionArg = !string.IsNullOrEmpty(subscription) 
                ? $" --subscription \"{subscription}\"" 
                : "";
            
            return $"deployment sub what-if --location \"{location}\"{subscriptionArg} --template-file \"{templateFile}\"{parametersArg} --no-prompt --no-pretty-print -o json";
        }
        else
        {
            // Defensive check: resource-group scope deployments require a resource group
            if (string.IsNullOrWhiteSpace(resourceGroup))
            {
                throw new ArgumentException("Resource group is required for resource-group scope deployments.", nameof(resourceGroup));
            }

            // Resource-group scope deployment: az deployment group what-if
            return $"deployment group what-if --resource-group \"{resourceGroup}\" --template-file \"{templateFile}\"{parametersArg} --no-prompt --no-pretty-print -o json";
        }
    }

    private DriftDetectionResult ParseWhatIfJson(string jsonOutput)
    {
        var result = new DriftDetectionResult();
        
        try
        {
            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            // The what-if output has a "changes" array
            if (!root.TryGetProperty("changes", out var changes))
            {
                Console.WriteLine("📝 No changes found in what-if output");
                return result;
            }

            foreach (var change in changes.EnumerateArray())
            {
                var resourceDrift = ParseResourceChange(change);
                if (resourceDrift != null && resourceDrift.PropertyDrifts.Count > 0)
                {
                    result.ResourceDrifts.Add(resourceDrift);
                }
            }

            result.HasDrift = result.ResourceDrifts.Any();
            result.Summary = result.HasDrift
                ? $"Configuration drift detected in {result.ResourceDrifts.Count} resource(s) with {result.ResourceDrifts.Sum(r => r.PropertyDrifts.Count)} property difference(s)."
                : "No configuration drift detected.";

            Console.WriteLine($"📦 Parsed {result.ResourceDrifts.Count} resources with drift from what-if JSON");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️ Failed to parse what-if JSON: {ex.Message}");
        }

        return result;
    }

    private ResourceDrift? ParseResourceChange(JsonElement change)
    {
        // Get the change type for the resource
        var changeType = change.GetProperty("changeType").GetString() ?? "";
        
        // Skip resources with no changes
        if (changeType == "NoChange" || changeType == "Ignore")
        {
            return null;
        }

        // Extract resource info
        var resourceId = change.TryGetProperty("resourceId", out var ridProp) ? ridProp.GetString() ?? "" : "";
        var (resourceType, resourceName) = ParseResourceId(resourceId);

        var drift = new ResourceDrift
        {
            ResourceType = resourceType,
            ResourceName = resourceName,
            ResourceId = resourceId,
            PropertyDrifts = new List<PropertyDrift>()
        };

        // Handle different change types
        switch (changeType)
        {
            case "Create":
                // Resource will be created (missing in Azure)
                drift.PropertyDrifts.Add(new PropertyDrift
                {
                    PropertyPath = "resource",
                    ExpectedValue = "defined in template",
                    ActualValue = "missing in Azure",
                    Type = DriftType.Missing
                });
                break;

            case "Delete":
                // Resource will be deleted (extra in Azure, not in template)
                drift.PropertyDrifts.Add(new PropertyDrift
                {
                    PropertyPath = "resource",
                    ExpectedValue = "not defined in template",
                    ActualValue = "exists in Azure",
                    Type = DriftType.Extra
                });
                break;

            case "Modify":
                // Resource has property changes
                if (change.TryGetProperty("delta", out var delta))
                {
                    ParsePropertyChanges(delta, drift);
                }
                break;

            case "Deploy":
                // Resource will be deployed but no property changes
                // This is often for nested deployments, we may want to skip
                break;
        }

        return drift;
    }

    private void ParsePropertyChanges(JsonElement delta, ResourceDrift drift)
    {
        foreach (var prop in delta.EnumerateArray())
        {
            var propertyChangeType = prop.TryGetProperty("propertyChangeType", out var pctProp) 
                ? pctProp.GetString() ?? "" 
                : "";
            
            // Skip NoEffect changes - they're not real drift
            if (propertyChangeType == "NoEffect")
            {
                continue;
            }

            var path = prop.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? "" : "";
            var before = GetPropertyValue(prop, "before");
            var after = GetPropertyValue(prop, "after");
            var hasChildren = prop.TryGetProperty("children", out var children) && 
                              children.ValueKind == JsonValueKind.Array && 
                              children.GetArrayLength() > 0;

            // Skip container nodes and only process their children
            // Container nodes are:
            // 1. Array type changes (the array itself has changes, real changes are in children)
            // 2. Modify type changes with children and null before/after (intermediate path segments like "0", "5")
            var isContainerNode = hasChildren && 
                                   (propertyChangeType == "Array" || 
                                    (string.IsNullOrEmpty(before) && string.IsNullOrEmpty(after)));
            
            if (isContainerNode)
            {
                ParseNestedPropertyChanges(children, drift, path);
                continue;
            }

            // Determine drift type based on change type
            var driftType = propertyChangeType switch
            {
                "Create" => DriftType.Missing,  // Template will add this (missing in Azure)
                "Delete" => DriftType.Extra,    // Template will remove this (extra in Azure)
                "Modify" => DriftType.Modified,
                "Array" => DriftType.Modified,  // Array without children (shouldn't happen often)
                _ => DriftType.Modified
            };

            // For Delete, swap before/after for clarity (what Azure has vs template expects)
            string expectedValue, actualValue;
            if (propertyChangeType == "Delete")
            {
                expectedValue = "not set";
                actualValue = before;
            }
            else if (propertyChangeType == "Create")
            {
                expectedValue = after;
                actualValue = "not set";
            }
            else
            {
                expectedValue = after;  // What template wants
                actualValue = before;   // What Azure has
            }

            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = path,
                ExpectedValue = expectedValue,
                ActualValue = actualValue,
                Type = driftType
            });

            // Recursively handle any remaining nested changes
            if (hasChildren)
            {
                ParseNestedPropertyChanges(children, drift, path);
            }
        }
    }

    private static void ParseNestedPropertyChanges(JsonElement children, ResourceDrift drift, string parentPath)
    {
        foreach (var child in children.EnumerateArray())
        {
            var propertyChangeType = child.TryGetProperty("propertyChangeType", out var pctProp) 
                ? pctProp.GetString() ?? "" 
                : "";
            
            if (propertyChangeType == "NoEffect")
            {
                continue;
            }

            var path = child.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? "" : "";
            var fullPath = string.IsNullOrEmpty(parentPath) ? path : $"{parentPath}.{path}";
            var before = GetPropertyValue(child, "before");
            var after = GetPropertyValue(child, "after");

            // Check if this has children
            var hasNestedChildren = child.TryGetProperty("children", out var nestedChildren) && 
                                     nestedChildren.ValueKind == JsonValueKind.Array && 
                                     nestedChildren.GetArrayLength() > 0;

            // Skip container nodes and only process their children
            // Container nodes are:
            // 1. Array type changes (the array itself has changes, real changes are in children)
            // 2. Modify type changes with children and null before/after (intermediate path segments like "0", "5")
            var isContainerNode = hasNestedChildren && 
                                   (propertyChangeType == "Array" || 
                                    (string.IsNullOrEmpty(before) && string.IsNullOrEmpty(after)));
            
            if (isContainerNode)
            {
                ParseNestedPropertyChanges(nestedChildren, drift, fullPath);
                continue;
            }

            var driftType = propertyChangeType switch
            {
                "Create" => DriftType.Missing,
                "Delete" => DriftType.Extra,
                "Modify" => DriftType.Modified,
                "Array" => DriftType.Modified,
                _ => DriftType.Modified
            };

            string expectedValue, actualValue;
            if (propertyChangeType == "Delete")
            {
                expectedValue = "not set";
                actualValue = before;
            }
            else if (propertyChangeType == "Create")
            {
                expectedValue = after;
                actualValue = "not set";
            }
            else
            {
                expectedValue = after;
                actualValue = before;
            }

            drift.PropertyDrifts.Add(new PropertyDrift
            {
                PropertyPath = fullPath,
                ExpectedValue = expectedValue,
                ActualValue = actualValue,
                Type = driftType
            });

            // Continue recursively for non-container types that might still have children
            if (hasNestedChildren)
            {
                ParseNestedPropertyChanges(nestedChildren, drift, fullPath);
            }
        }
    }

    private static string GetPropertyValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return "";
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            JsonValueKind.Object => prop.GetRawText(),
            JsonValueKind.Array => prop.GetRawText(),
            _ => prop.GetRawText()
        };
    }

    private static (string resourceType, string resourceName) ParseResourceId(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return ("Unknown", "Unknown");
        }

        // Resource ID format: /subscriptions/{sub}/resourceGroups/{rg}/providers/{provider}/{type}/{name}
        // Or for resource groups: /subscriptions/{sub}/resourceGroups/{rgName}
        var parts = resourceId.Split('/');
        
        // Find the providers index
        var providersIndex = Array.IndexOf(parts, "providers");
        var resourceGroupsIndex = Array.IndexOf(parts, "resourceGroups");
        
        // If we have resourceGroups but no providers, this IS a resource group resource
        if (resourceGroupsIndex >= 0 && providersIndex < 0)
        {
            // Format: /subscriptions/{sub}/resourceGroups/{rgName}
            if (resourceGroupsIndex + 1 < parts.Length)
            {
                var rgName = parts[resourceGroupsIndex + 1];
                if (string.IsNullOrWhiteSpace(rgName))
                {
                    return ("Microsoft.Resources/resourceGroups", "Unknown");
                }
                return ("Microsoft.Resources/resourceGroups", rgName);
            }
            return ("Microsoft.Resources/resourceGroups", "Unknown");
        }
        
        if (providersIndex < 0 || providersIndex + 3 > parts.Length)
        {
            return ("Unknown", resourceId);
        }

        // Extract resource type and name
        // Handle nested resources like Microsoft.Storage/storageAccounts/blobServices
        // Resource ID format after providers: Microsoft.Network/virtualNetworks/vnetName/subnets/subnetName
        // The pattern is: provider/type/name[/childType/childName]...
        var remainingParts = parts.Skip(providersIndex + 1).ToArray();
        
        if (remainingParts.Length >= 2)
        {
            // First part is always the provider namespace (e.g., Microsoft.Network)
            var provider = remainingParts[0];
            
            // Build type and name from remaining parts
            // Pattern: type/name/childType/childName/...
            var typeParts = new List<string> { provider };
            var nameParts = new List<string>();
            
            for (int i = 1; i < remainingParts.Length; i++)
            {
                if (i % 2 == 1)
                {
                    // Odd indices (1, 3, 5...) are types
                    typeParts.Add(remainingParts[i]);
                }
                else
                {
                    // Even indices (2, 4, 6...) are names
                    nameParts.Add(remainingParts[i]);
                }
            }
            
            return (string.Join("/", typeParts), string.Join("/", nameParts));
        }

        return ("Unknown", resourceId);
    }

    // here we start seaching for the using statement in the bicepparam file, 
    // so we can extract the bicep file belonging to the bicepparam
    private static async Task<string> GetReferencedBicepFileAsync(string bicepparamFilePath)
    {
        // read the whole file and split lines by \n
        var content = await File.ReadAllTextAsync(bicepparamFilePath);
        var lines = content.Split('\n');
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim(); // trim in case someone indented the using statement.
            if (trimmed.StartsWith("using"))
            {
                // Extract the file path from: using 'file.bicep' or using './file.bicep'
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"using\s+'([^']+)'");
                if (match.Success)
                {
                    var referencedFile = match.Groups[1].Value; // here we get the filename of the bicepfile
                    var directory = Path.GetDirectoryName(Path.GetFullPath(bicepparamFilePath)) ?? "";
                    var fullPath = Path.GetFullPath(Path.Combine(directory, referencedFile));
                    
                    // Security check: ensure the resolved path is within the current working directory tree
                    // This allows relative paths like ../../../deployments/infra.bicep within a repo
                    // but prevents access to files outside the repo (e.g., /etc/passwd)
                    var workingDirectory = Path.GetFullPath(Environment.CurrentDirectory);
                    if (!fullPath.StartsWith(workingDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new UnauthorizedAccessException(
                            $"Security violation: Referenced file '{referencedFile}' resolves to '{fullPath}' " +
                            $"which is outside the allowed directory '{workingDirectory}'.");
                    }
                    
                    if (!File.Exists(fullPath))
                    {
                        throw new FileNotFoundException($"Referenced file '{referencedFile}' does not exist at resolved path '{fullPath}'.");
                    }
                    if ((File.GetAttributes(fullPath) & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        throw new InvalidOperationException($"Referenced path '{fullPath}' is a directory, not a file.");
                    }
                    
                    return fullPath;
                }
            }
        }
        
        throw new InvalidOperationException($"Could not find 'using' statement in {bicepparamFilePath}");
    }


}
