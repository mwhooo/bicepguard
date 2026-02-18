namespace DriftGuard.CLI;

/// <summary>
/// Validates command-line inputs and arguments.
/// All methods are static as they don't maintain state.
/// </summary>
public static class InputValidator
{
    /// <summary>
    /// Validates that bicepparam and parameters file are not both specified.
    /// A .bicepparam file provided via --bicep-file cannot be combined with --parameters-file.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateBicepParamsSpecified(
        FileInfo bicepFile,
        FileInfo? parametersFile)
    {
        if (parametersFile != null &&
            string.Equals(bicepFile.Extension, ".bicepparam", StringComparison.OrdinalIgnoreCase))
        {
            //ConsoleOutput.WriteError($"Cannot use a .bicepparam file via --bicep-file together with --parameters-file. Please specify only the .bicepparam file", simpleOutput: true);
            return (false, "Cannot use a .bicepparam file via --bicep-file together with --parameters-file. Please specify only the .bicepparam file");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates the parameters file if provided.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateParametersFile(FileInfo? parametersFile)
    {
        if (parametersFile == null)
            return (true, null);

        if (!parametersFile.Exists)
        {
            return (false, $"Parameters file not found: {parametersFile.FullName}");
        }

        if (!parametersFile.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Parameters file must have a .json extension: {parametersFile.FullName}, bicepparams can be fed directly via --bicep-file");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates resource group is specified when using ResourceGroup scope.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateResourceGroupScope(string? resourceGroup)
    {
        if (string.IsNullOrWhiteSpace(resourceGroup))
        {
            return (false, "--resource-group is required for ResourceGroup scope");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates subscription and location are specified when using Subscription scope.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateSubscriptionScope(
        string? subscription,
        string? location)
    {
        if (string.IsNullOrWhiteSpace(subscription))
        {
            return (false, "--subscription is required for Subscription scope");
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            return (false, "--location is required for Subscription scope");
        }

        return (true, null);
    }
}
