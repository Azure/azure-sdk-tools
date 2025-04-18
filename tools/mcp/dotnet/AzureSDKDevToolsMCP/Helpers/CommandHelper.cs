using System.Runtime.InteropServices;

namespace AzureSDKDevToolsMCP.Helpers
{
    public interface ICommandHelper
    {
        // Define methods that will be implemented in CommandHelper
        string GetCommandFilePath(string commandName);
    }
    public class CommandHelper: ICommandHelper
    {
        // Implement the methods defined in ICommandHelper
        public string GetCommandFilePath(string commandName)
        {
            // Get all paths in environment variable
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
            if (paths != null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Logic to get the command file path based on the command name for Windows
                    foreach (var path in paths)
                    {
                        var commandPath = Path.Combine(path, $"{commandName}.exe");
                        if (File.Exists(commandPath))
                        {
                            return commandPath;
                        }
                    }
                }
                else
                {
                    // Logic to get the command file path based on the command name for non-Windows
                    foreach (var path in paths)
                    {
                        var commandPath = Path.Combine(path, commandName);
                        if (File.Exists(commandPath))
                        {
                            return commandPath;
                        }
                    }
                }
            }
            return string.Empty;
        }
    }
}
