using System.Runtime.InteropServices;

namespace Azure.Tools.GeneratorAgent.Security
{
    /// <summary>
    /// Provides secure, hardcoded process execution configuration for cross-platform support.
    /// No user-configurable values to prevent security vulnerabilities.
    /// </summary>
    internal static class SecureProcessConfiguration
    {
        /// <summary>
        /// Gets the PowerShell Core executable name (cross-platform).
        /// </summary>
        public static string PowerShellExecutable => "pwsh";
        
        /// <summary>
        /// Gets the Node.js executable name.
        /// </summary>
        public static string NodeExecutable => "node";
        
        /// <summary>
        /// Gets the NPM executable name.
        /// On Windows, use PowerShell to execute npm for reliable execution. On other platforms, use npm.
        /// </summary>
        public static string NpmExecutable { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? PowerShellExecutable : "npm";
        
        /// <summary>
        /// Gets the NPX executable name.
        /// On Windows, use PowerShell to execute npx for reliable execution. On other platforms, use npx.
        /// </summary>
        public static string NpxExecutable { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? PowerShellExecutable : "npx";
        
        /// <summary>
        /// Gets the .NET CLI executable name.
        /// </summary>
        public static string DotNetExecutable => "dotnet";

        /// <summary>
        /// Gets the Git executable name.
        /// </summary>
        public static string GitExecutable => "git";

        /// <summary>
        /// Gets the Bash executable name.
        /// On Windows, this may require Git Bash or WSL. On other platforms, use bash.
        /// </summary>
        public static string BashExecutable => "bash";

        /// <summary>
        /// Gets the set of allowed commands for security validation.
        /// Includes platform-specific variations for cross-platform compatibility.
        /// </summary>
        public static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            PowerShellExecutable,
            NodeExecutable,
            "npm",      // Unix/Linux/macOS
            "npm.cmd",  // Windows
            "npx",      // Unix/Linux/macOS
            "npx.cmd",  // Windows
            DotNetExecutable,
            "git",      // Git command for repository operations
            "bash",     // Bash shell for scripting operations
            "-Command"  // PowerShell parameter
        };        /// <summary>
        /// Validates if a command is allowed to be executed.
        /// </summary>
        /// <param name="command">The command to validate</param>
        /// <returns>True if the command is allowed, false otherwise</returns>
        public static bool IsCommandAllowed(string command)
        {
            return !string.IsNullOrWhiteSpace(command) && AllowedCommands.Contains(command);
        }
    }
}
