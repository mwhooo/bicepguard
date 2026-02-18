using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace BicepGuard.Services;

public class BicepService
{
    private const int MaxRecursionDepth = 10; // we might want to expose this as an external configuration.

    public List<JObject> ExtractResourcesFromTemplate(JObject armTemplate)
    {
        var resources = new List<JObject>();
        
        if (armTemplate["resources"] is JArray resourceArray)
        {
            // Traditional ARM template format - resources as array
            Console.WriteLine($"🔍 Found {resourceArray.Count} resources in ARM template (array format)");
            foreach (var resource in resourceArray)
            {
                if (resource is JObject resourceObj)
                {
                    ProcessResource(resourceObj, armTemplate, resources);
                }
            }
        }
        else if (armTemplate["resources"] is JObject resourceObject)
        {
            // Bicep 2.0 format - resources as object with named keys
            Console.WriteLine($"🔍 Found {resourceObject.Properties().Count()} resources in ARM template (object format)");
            foreach (var resourceProperty in resourceObject.Properties())
            {
                if (resourceProperty.Value is JObject resourceObj)
                {
                    // Add the resource key as a property for reference
                    resourceObj["_resourceKey"] = resourceProperty.Name;
                    ProcessResource(resourceObj, armTemplate, resources);
                }
            }
        }
        else
        {
            Console.WriteLine($"⚠️  No resources found in ARM template");
        }

        Console.WriteLine($"🎯 Total extracted resources: {resources.Count}");
        return resources;
    }

    private void ProcessResource(JObject resourceObj, JObject armTemplate, List<JObject> resources)
    {
        var resourceType = resourceObj["type"]?.ToString();
        var resourceName = resourceObj["name"]?.ToString();
        var resourceKey = resourceObj["_resourceKey"]?.ToString();
        Console.WriteLine($"  📋 Processing resource: {resourceType} - {resourceName ?? resourceKey}");
        
        // Check if resource has a condition and evaluate it
        if (ShouldResourceBeDeployed(resourceObj, armTemplate))
        {
            Console.WriteLine($"    ✅ Resource should be deployed");
            
            // Check if this is a module deployment (nested template)
            if (resourceType == "Microsoft.Resources/deployments" && HasNestedTemplate(resourceObj))
            {
                Console.WriteLine($"    🔄 Extracting resources from module deployment");
                // Extract resources from the nested template instead of the deployment resource itself
                var nestedResources = ExtractResourcesFromModuleDeployment(resourceObj);
                Console.WriteLine($"    📦 Found {nestedResources.Count} nested resources");
                resources.AddRange(nestedResources);
            }
            else
            {
                // Regular resource - add it normally
                Console.WriteLine($"    📝 Adding regular resource");
                resources.Add(resourceObj);
            }
        }
        else
        {
            Console.WriteLine($"    ❌ Resource should NOT be deployed (condition evaluated to false)");
        }
    }

    private bool HasNestedTemplate(JObject deploymentResource)
    {
        // Check if this deployment has a nested template (indicating it's a module)
        var template = deploymentResource["properties"]?["template"];
        var templateLink = deploymentResource["properties"]?["templateLink"];
        return template != null || templateLink != null;
    }

    private List<JObject> ExtractResourcesFromModuleDeployment(JObject deploymentResource)
    {
        var nestedResources = new List<JObject>();
        
        try
        {
            // Get the nested template and parameters from the deployment
            var nestedTemplate = deploymentResource["properties"]?["template"] as JObject;
            var templateLink = deploymentResource["properties"]?["templateLink"];
            var deploymentParameters = deploymentResource["properties"]?["parameters"] as JObject;
            
            if (nestedTemplate != null)
            {
                // Handle inline templates (local modules)
                Console.WriteLine($"    📦 Processing inline module template");
                
                // Note: External modules are now handled by what-if analysis in the main conversion method
                // This provides more accurate resolution of external module references
                
                // Create a context with resolved parameters for parameter substitution
                var parameterContext = CreateParameterContext(deploymentParameters ?? new JObject(), nestedTemplate);
                
                // Recursively extract resources from the nested template
                var extractedResources = ExtractResourcesFromTemplate(nestedTemplate);
                
                // Resolve parameters in the extracted resources
                foreach (var resource in extractedResources)
                {
                    // Resolve parameter expressions in the resource
                    var resolvedResource = ResolveParametersInResource(resource, parameterContext);
                    
                    // Add module context for better identification
                    var moduleName = deploymentResource["name"]?.ToString() ?? "unknown-module";
                    resolvedResource["_moduleDeploymentName"] = moduleName;
                    nestedResources.Add(resolvedResource);
                }
            }
            else if (templateLink != null)
            {
                // External module reference - we can't extract resources without downloading the template
                var moduleName = deploymentResource["name"]?.ToString() ?? "unknown-module";
                var templateUri = templateLink["uri"]?.ToString() ?? "unknown-uri";
                
                Console.WriteLine($"    🌐 External module reference detected: {moduleName}");
                Console.WriteLine($"      📍 Template URI: {templateUri}");
                Console.WriteLine($"      ⚠️  Cannot extract individual resources from external registry modules");
                Console.WriteLine($"      💡 Recommendation: Use 'az deployment group what-if' for external module analysis");
                
                // Return empty list - don't include the deployment resource itself
                return nestedResources;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not extract resources from module deployment: {ex.Message}");
            
            // Check if this was an external module before falling back
            var templateLink = deploymentResource["properties"]?["templateLink"];
            if (templateLink != null)
            {
                // Don't fall back to including the deployment resource for external modules
                Console.WriteLine($"    🚫 Skipping external module due to extraction error");
                return nestedResources;
            }
            else
            {
                // For inline modules, fall back to including the deployment resource
                // This ensures we don't lose track of the resource entirely
                nestedResources.Add(deploymentResource);
            }
        }
        
        return nestedResources;
    }



    private Dictionary<string, JToken> CreateParameterContext(JObject? deploymentParameters, JObject nestedTemplate)
    {
        var context = new Dictionary<string, JToken>();
        
        if (deploymentParameters != null)
        {
            foreach (var param in deploymentParameters)
            {
                var value = param.Value?["value"];
                if (value != null)
                {
                    context[param.Key] = value;
                }
            }
        }
        
        return context;
    }

    private JObject ResolveParametersInResource(JObject resource, Dictionary<string, JToken> parameterContext)
    {
        // Create a deep copy to avoid modifying the original
        var resolvedResource = (JObject)resource.DeepClone();
        
        // Recursively resolve parameter references
        ResolveParametersInToken(resolvedResource, parameterContext);
        
        return resolvedResource;
    }

    private void ResolveParametersInToken(JToken token, Dictionary<string, JToken> parameterContext)
    {
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties().ToList())
            {
                ResolveParametersInToken(property.Value, parameterContext);
            }
        }
        else if (token is JArray array)
        {
            foreach (var item in array)
            {
                ResolveParametersInToken(item, parameterContext);
            }
        }
        else if (token is JValue value && value.Type == JTokenType.String)
        {
            var stringValue = value.Value?.ToString();
            if (!string.IsNullOrEmpty(stringValue) && stringValue.StartsWith("[parameters('") && stringValue.EndsWith("')]"))
            {
                // Extract parameter name from [parameters('paramName')]
                var paramName = stringValue.Substring(13, stringValue.Length - 16);
                if (parameterContext.TryGetValue(paramName, out var resolvedValue))
                {
                    // Replace with resolved value
                    var parent = value.Parent;
                    if (parent is JProperty prop)
                    {
                        prop.Value = resolvedValue;
                    }
                }
            }
        }
    }

    private bool ShouldResourceBeDeployed(JObject resource, JObject armTemplate)
    {
        // If there's no condition property, the resource should always be deployed
        if (resource["condition"] == null)
        {
            return true;
        }

        var condition = resource["condition"]?.ToString();
        if (string.IsNullOrEmpty(condition))
        {
            return true;
        }

        // Evaluate the condition expression
        return EvaluateConditionExpression(condition, armTemplate);
    }

    private bool EvaluateConditionExpression(string condition, JObject armTemplate)
    {
        try
        {
            // Handle simple variable references like "[variables('deploykeyvault')]"
            if (condition.StartsWith("[variables('") && condition.EndsWith("')]"))
            {
                var variableName = condition.Substring(12, condition.Length - 15); // Extract variable name
                var variables = armTemplate["variables"] as JObject;
                
                if (variables != null && variables[variableName] != null)
                {
                    var variableValue = variables[variableName];
                    
                    // Handle boolean values
                    if (variableValue?.Type == JTokenType.Boolean)
                    {
                        return variableValue.Value<bool>();
                    }
                    
                    // Handle string representations of boolean
                    if (variableValue?.Type == JTokenType.String)
                    {
                        var stringValue = variableValue.Value<string>()?.ToLowerInvariant();
                        return stringValue == "true";
                    }
                }
            }
            
            // Handle simple parameter references like "[parameters('deploykeyvault')]"
            if (condition.StartsWith("[parameters('") && condition.EndsWith("')]"))
            {
                var parameterName = condition.Substring(13, condition.Length - 16); // Extract parameter name
                var parameters = armTemplate["parameters"] as JObject;
                
                if (parameters != null && parameters[parameterName] != null)
                {
                    var parameter = parameters[parameterName] as JObject;
                    var defaultValue = parameter?["defaultValue"];
                    
                    if (defaultValue?.Type == JTokenType.Boolean)
                    {
                        return defaultValue.Value<bool>();
                    }
                    
                    if (defaultValue?.Type == JTokenType.String)
                    {
                        var stringValue = defaultValue.Value<string>()?.ToLowerInvariant();
                        return stringValue == "true";
                    }
                }
            }

            // For more complex expressions, default to true (deploy the resource)
            // This is a conservative approach - if we can't evaluate the condition,
            // we include the resource and let the comparison logic handle it
            Console.WriteLine($"⚠️  Could not evaluate condition: {condition}. Assuming resource should be deployed.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error evaluating condition '{condition}': {ex.Message}. Assuming resource should be deployed.");
            return true;
        }
    }

    private static string GetAzureCLIPath()
    {
        return AzureCliPathResolver.GetAzureCLIPath();
    }
}