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
            storageLocationOption.AddAlias("-f");

            var storagePluginOption = new Option<string>(
                name: "--storage-plugin",
                description: "The plugin for the selected storage, default is Git storage is GitStore. (Currently the only option)",
                getDefaultValue: () => "GitStore");
            storagePluginOption.AddAlias("-l");

            var assetsJsonPathOption = new Option<string>(
                name: "--assets-json-path",
                description: "Required for Push/Reset/Restore. This should be a path to a valid assets.json within a language repository.",
                getDefaultValue: () => null)
            {
                IsRequired = true
            };
            assetsJsonPathOption.AddAlias("-a");

            var confirmResetOption = new Option<string>(
                name: "--yes",
                description: "Do not prompt for confirmation when resetting pending changes.",
                getDefaultValue: () => null);
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
            #endregion

            var root = new RootCommand();
            root.AddGlobalOption(storageLocationOption);
            root.AddGlobalOption(storagePluginOption);

            root.SetHandler(async (defaultOpts) =>
                {
                    await callback(defaultOpts);
                },
                new DefaultOptsBinder(storageLocationOption, storagePluginOption)
            );

            var startCommand = new Command("start", "Start the TestProxy.");
            startCommand.AddOption(insecureOption);
            startCommand.AddOption(dumpOption);
            startCommand.SetHandler(async (startOpts) =>
                {
                    await callback(startOpts);
                },
                new StartOptionsBinder(storageLocationOption, storagePluginOption, insecureOption, dumpOption)
            );
            root.Add(startCommand);

            var pushCommand = new Command("push", "Push the assets, referenced by assets.json, into git.");
            pushCommand.AddOption(assetsJsonPathOption);
            pushCommand.SetHandler(async (pushOpts) =>
                {
                    await callback(pushOpts);
                },
                new PushOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption)
            );
            root.Add(pushCommand);

            var restoreCommand = new Command("restore", "Restore the assets, referenced by assets.json, from git.");
            restoreCommand.AddOption(assetsJsonPathOption);
            restoreCommand.SetHandler(async (restoreOpts) =>
            {
                await callback(restoreOpts);
            },
                new RestoreOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption)
            );
            root.Add(restoreCommand);

            var resetCommand = new Command("reset", "Reset the assets, referenced by assets.json, from git to their original files referenced by the tag. Will prompt if there are pending changes unless indicated by -y/--yes.");
            restoreCommand.AddOption(assetsJsonPathOption);
            restoreCommand.AddOption(confirmResetOption);
            resetCommand.SetHandler(async (resetOpts) =>
                {
                    await callback(resetOpts);
                },
                new ResetOptionsBinder(storageLocationOption, storagePluginOption, assetsJsonPathOption, confirmResetOption)
            );
            root.Add(resetCommand);

            return root;
        }
    }
}
