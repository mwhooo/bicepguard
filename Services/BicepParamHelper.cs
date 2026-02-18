using System.Text.RegularExpressions;

namespace BicepGuard.Services;

/// <summary>
/// Shared helper for parsing .bicepparam files.
/// Extracts the referenced .bicep file from the 'using' statement with security validation.
/// </summary>
public static class BicepParamHelper
{
    /// <summary>
    /// Reads a .bicepparam file and resolves the referenced .bicep file path from its 'using' statement.
    /// Includes security checks to prevent path traversal outside the working directory.
    /// </summary>
    public static async Task<string> GetReferencedBicepFileAsync(string bicepparamFilePath)
    {
        var content = await File.ReadAllTextAsync(bicepparamFilePath);
        var lines = content.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("using"))
            {
                var match = Regex.Match(trimmed, @"using\s+'([^']+)'");
                if (match.Success)
                {
                    var referencedFile = match.Groups[1].Value;
                    var directory = Path.GetDirectoryName(Path.GetFullPath(bicepparamFilePath)) ?? "";
                    var fullPath = Path.GetFullPath(Path.Combine(directory, referencedFile));

                    // Security check: ensure the resolved path is within the current working directory tree
                    var workingDirectory = Path.GetFullPath(Environment.CurrentDirectory);
                    if (!fullPath.StartsWith(workingDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new UnauthorizedAccessException(
                            $"Security violation: Referenced file '{referencedFile}' resolves to '{fullPath}' " +
                            $"which is outside the allowed directory '{workingDirectory}'.");
                    }

                    if (!File.Exists(fullPath))
                    {
                        throw new FileNotFoundException(
                            $"Referenced file '{referencedFile}' does not exist at resolved path '{fullPath}'.");
                    }

                    if ((File.GetAttributes(fullPath) & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        throw new InvalidOperationException(
                            $"Referenced path '{fullPath}' is a directory, not a file.");
                    }

                    return fullPath;
                }
            }
        }

        throw new InvalidOperationException($"Could not find 'using' statement in {bicepparamFilePath}");
    }
}
