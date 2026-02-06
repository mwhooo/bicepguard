using System.Diagnostics;

namespace DriftGuard.Services;

/// <summary>
/// Provides cross-platform Azure CLI path resolution.
/// </summary>
public static class AzureCliPathResolver
{
    private const int AzCliVersionCheckTimeoutMs = 5000;

    /// <summary>
    /// Gets the path to the Azure CLI executable, supporting both Linux and Windows.
    /// </summary>
    /// <returns>The path to the Azure CLI executable.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Azure CLI cannot be found.</exception>
    public static string GetAzureCLIPath()
    {
        // On Linux/Docker, try common Linux paths first
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            var linuxPaths = new[] { "/usr/bin/az", "/usr/local/bin/az", "az" };
            foreach (var path in linuxPaths)
            {
                if (IsCommandAvailable(path))
                {
                    return path;
                }
            }
        }

        // On Windows, try to find az using 'where' command (most reliable)
        try
        {
            using var whereProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "az",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            whereProcess.Start();
            var output = whereProcess.StandardOutput.ReadToEnd();
            whereProcess.WaitForExit(AzCliVersionCheckTimeoutMs);
            
            if (whereProcess.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var paths = output.Trim().Split('\n', '\r');
                // Prefer .cmd files over batch files, filter out empty lines
                var validPaths = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
                var preferredPath = validPaths.FirstOrDefault(p => p.Trim().EndsWith(".cmd")) ?? validPaths.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(preferredPath))
                {
                    return preferredPath.Trim();
                }
            }
        }
        catch
        {
            // Fall back to manual search
        }

        // Try common Windows Azure CLI locations
        var possiblePaths = new[]
        {
            @"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd",
            @"C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd",
            "az",
            "az.exe",
            @"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.exe"
        };

        foreach (var path in possiblePaths)
        {
            if (IsCommandAvailable(path))
            {
                return path;
            }
        }

        throw new InvalidOperationException(
            "Azure CLI not found. Please install Azure CLI from https://docs.microsoft.com/cli/azure/install-azure-cli");
    }

    /// <summary>
    /// Checks if a command is available by attempting to execute it with --version.
    /// </summary>
    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit(AzCliVersionCheckTimeoutMs);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
