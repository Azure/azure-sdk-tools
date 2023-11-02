using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.Threading.Tasks;
using static System.CommandLine.Help.HelpBuilder;

namespace Azure.Sdk.Tools.TestProxy.CommandOptions
{
    public static class OptionsGenerator
    {
        public static RootCommand GenerateCommandLineOptions(Func<DefaultOptions, Task> callback)
        {
            #region option definitions
            var storageLocationOption = new Option<string>(
                name: "--storage-location",
                description: "The path to the target local git repo. If not provided as an argument, Environment variable TEST_PROXY_FOLDER will be consumed. Lacking both, the current working directory will be utilized.",
                getDefaultValue: () => null);
            storageLocationOption.AddAlias("-l");

            var storagePluginOption = new Option<string>(
                name: "--storage-plugin",
                description: "The plugin for the selected storage, default is Git storage is GitStore. (Currently the only option)",
                getDefaultValue: () => "GitStore");
            storagePluginOption.AddAlias("-p");

            var assetsJsonPathOption = new Option<string>(
                name: "--assets-json-path",
                description: "Required for any operation that requires an assets.json path. Currently Push/Reset/Restore. This should be a path to a valid assets.json within a language repository.")
            {
                IsRequired = true
            };
            assetsJsonPathOption.AddAlias("-a");

            var confirmResetOption = new Option<bool>(
                name: "--yes",
                description: "Do not prompt for confirmation when resetting pending changes.",
                getDefaultValue: () => false);
            confirmResetOption.AddAlias("-y");

            var insecureOption = new Option<bool>(
                name: "--insecure",
                description: "Flag; Allow insecure upstream SSL certs.",
                getDefaultValue: () => false);
            insecureOption.AddAlias("-i");

            var dumpOption = new Option<bool>(
                name: "--dump",
                description: "Flag; Output configuration values when starting the Test-Proxy.",
                getDefaultValue: () => false);
            dumpOption.AddAlias("-d");

            var universalOption = new Option<bool>(
                name: "--universalOutput",
                description: "Flag; Redirect all logs to stdout, including what would normally be showing up on stderr.",
                getDefaultValue: () => false);
            dumpOption.AddAlias("-u");


            var collectedArgs = new Argument<string[]>("args")
            {
                Arity = ArgumentArity.ZeroOrMore,
                Description = "Remaining arguments after \"--\". Used for asp.net arguments."
            };
            #endregion


            var Description = @"This tool is used by the Azure SDK team in two primary ways:

  - Run as a http record/playback server. (""start"" / default verb)
  - Invoke a CLI Tool to interact with recordings in an external store. (""push"", ""restore"", ""reset"", ""config"")";

            var root = new RootCommand()
            {
                Description = Description
            };
            root.AddGlobalOption(storageLocationOption);
            root.AddGlobalOption(storagePluginOption);

            root.SetHandler(async (defaultOpts) => await callback(defaultOpts),
                new DefaultOptsBinder(storageLocationOption, storagePluginOption)
            );

            var startCommand = new Command("start", "Start the TestProxy.");
            startCommand.AddOption(insecureOption);
            startCommand.AddOption(dumpOption);
            startCommand.AddArgument(collectedArgs);

            startCommand.SetHandler(async (startOpts) => await callback(startOpts),
                new StartOptionsBinder(storageLocationOption, storagePluginOption, insecureOption, dumpOption, universalOption, collectedArgs)
            );
            root.Add(startCommand);

            var pushCommand = new Command("push", "Push the assets, referenced by assets.json, into git.");
            pushCommand.AddOption(assetsJsonPathOption);
            pushCommand.SetHandler(async (pushOpts) => await callback(pushOpts),
                new PushOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption)
            );
            root.Add(pushCommand);

            var restoreCommand = new Command("restore", "Restore the assets, referenced by assets.json, from git.");
            restoreCommand.AddOption(assetsJsonPathOption);
            restoreCommand.SetHandler(async (restoreOpts) => await callback(restoreOpts),
                new RestoreOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption)
            );
            root.Add(restoreCommand);

            var resetCommand = new Command("reset", "Reset the assets, referenced by assets.json, from git to their original files referenced by the tag. Will prompt if there are pending changes unless indicated by -y/--yes.");
            resetCommand.AddOption(assetsJsonPathOption);
            resetCommand.AddOption(confirmResetOption);
            resetCommand.SetHandler(async (resetOpts) => await callback(resetOpts),
                new ResetOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption, confirmResetOption)
            );
            root.Add(resetCommand);

            var configCommand = new Command("config", "Interact with an assets.json.");
            configCommand.SetHandler(async (configOpts) => await callback(configOpts),
                new ConfigOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption)
            );
            var configCreateCommand = new Command("create", "Enter a prompt and create an assets.json.");
            configCreateCommand.AddOption(assetsJsonPathOption);
            configCreateCommand.SetHandler(async (configOpts) => await callback(configOpts),
                new ConfigCreateOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption)
            );
            var configShowCommand = new Command("show", "Show the content of a given assets.json.");
            configShowCommand.AddOption(assetsJsonPathOption);
            configShowCommand.SetHandler(async (configOpts) => await callback(configOpts),
                new ConfigShowOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption)
            );
            var configLocateCommand = new Command("locate", "Get the assets repo root for a given assets.json path.");
            configLocateCommand.AddOption(assetsJsonPathOption);
            configLocateCommand.SetHandler(async (configOpts) => await callback(configOpts),
                new ConfigLocateOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption)
            );

            configCommand.AddCommand(configCreateCommand);
            configCommand.AddCommand(configShowCommand);
            configCommand.AddCommand(configLocateCommand);
            root.Add(configCommand);

            return root;
        }
    }
}
