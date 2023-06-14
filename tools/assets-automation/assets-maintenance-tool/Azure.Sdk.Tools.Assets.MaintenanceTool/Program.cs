using System.CommandLine;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Options;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Scan;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool;

public class Program
{
    public static string[] StoredArgs = new string[] { };

    // By default, we will only search under the CWD for the previous output. This may change
    // in the future, but we are keeping it simple to start.

    // all should honor
        // --scan-data <-- results from previous scans (todo, currently checks working directory)
        
    // SCAN
        // --configuration: -> path to file

    // BACKUP
        // --configuration provided?
            // SCAN
            // BACKUP
            // as each tag is backed up, it is saved with suffix _backup

    // RESTORE
        // --input-tag <tag that has been stored away>

    // CLEANUP
        // --configuration provided?
            // SCAN
            // BACKUP
            // CLEANUP
                // each tag as found by configuration

        // --input-tag <tag on repo>?
            // SCAN, BACKUP, and CLEANUP individual tag

    public static void Main(string[] args)
    {
        StoredArgs = args;
        
        var rootCommand = InitializeCommandOptions(Run);
        var resultCode = rootCommand.Invoke(args);
        Environment.Exit(resultCode);
    }

    public static void Run(object commandObj)
    {
        switch (commandObj)
        {
            case BaseOptions configOptions:
                AssetsScanner scanner = new AssetsScanner();
                var runConfig = new RunConfiguration(configOptions.ConfigLocation);
                AssetsResultSet results = scanner.Scan(runConfig);
                scanner.Save(results);

                break;
            default:
                throw new ArgumentException($"Unable to parse the argument set: {string.Join(" ", StoredArgs)}");
        }
    }

    public static RootCommand InitializeCommandOptions(Action<BaseOptions> action)
    {
        var root = new RootCommand();
        var configOption = new Option<string>(
            name: "--config",
            description: "The path to the json file containing the repo configuration. A sample repo configuration can be seen under <SolutionDirectory>/integration-test-repo-configuration.yml."
            ) {
                IsRequired = true
            };
        configOption.AddAlias("-c");

        var scanCommand = new Command("scan", "Scan the repositories as configured within the file provided to input argument --config <yml config file>.");
        scanCommand.AddOption(configOption);
        scanCommand.SetHandler(
            (configOpts) => action(configOpts),
            new BaseOptionsBinder(configOption)
        );
        root.Add(scanCommand);

        return root;
    }
}
