using Azure.Sdk.Tools.PerfAutomation.Models;
using CommandLine;
using CommandLine.Text;
using Microsoft.Crank.Agent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public static class Program
    {
        private static OptionsDefinition Options { get; set; }

        private class OptionsDefinition
        {
            [Option('d', "debug")]
            public bool Debug { get; set; }

            [Option('l', "languages")]
            public IEnumerable<Language> Languages { get; set; }

            [Option('i', "inputFile", Default = "input.yml")]
            public string InputFile { get; set; }

            [Option('o', "outputFile", Default = "output.csv")]
            public string OutputFile { get; set; }

            [Option('t', "testFilter", HelpText = "Regex of tests to run")]
            public string TestFilter { get; set; }

            [Option("workingDirectoryNet")]
            public string WorkingDirectoryNet { get; set; }
        }

        public static async Task Main(string[] args)
        {
            var parser = new CommandLine.Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = null;
            });

            var parserResult = parser.ParseArguments<OptionsDefinition>(args);

            await parserResult.MapResult(
                options => Run(options),
                errors => DisplayHelp(parserResult)
            );
        }

        static Task DisplayHelp<T>(ParserResult<T> result)
        {
            var helpText = HelpText.AutoBuild(result, settings =>
            {
                settings.AddEnumValuesToHelpText = true;
                return settings;
            });

            Console.Error.WriteLine(helpText);

            return Task.CompletedTask;
        }

        private static async Task Run(OptionsDefinition options)
        {
            Options = options;

            var parser = new MergingParser(new YamlDotNet.Core.Parser(File.OpenText(options.InputFile)));

            var deserializer = new Deserializer();
            var tests = deserializer.Deserialize<List<Test>>(parser);

            var selectedTests = tests.Where(t =>
                String.IsNullOrEmpty(options.TestFilter) || Regex.IsMatch(t.Name, options.TestFilter, RegexOptions.IgnoreCase));

            foreach (var test in selectedTests)
            {
                var selectedLanguages = test.Languages.Where(l => !options.Languages.Any() || options.Languages.Contains(l.Key));

                foreach (var language in selectedLanguages)
                {
                    foreach (var arguments in test.Arguments)
                    {
                        DebugWriteLine($"Test: {test.Name}, Language: {language.Key}, " +
                            $"TestName: {language.Value.TestName}, Arguments: {arguments}");
                        foreach (var packageVersions in language.Value.PackageVersions)
                        {
                            DebugWriteLine("===");
                            foreach (var packageVersion in packageVersions)
                            {
                                DebugWriteLine($"  Name: {packageVersion.Key}, Version: {packageVersion.Value}");
                            }


                            switch (language.Key)
                            {
                                case Language.Net:
                                    await RunNet(language.Value, arguments, packageVersions);
                                    break;
                                default:
                                    continue;
                            }
                        }
                    }

                }
            }
        }

        private static async Task RunNet(LanguageSettings languageSettings, string arguments, IDictionary<string, string> packageVersions)
        {
            var processArguments = $"run -c release -f netcoreapp2.1 -p {languageSettings.Project} -- " +
                $"{languageSettings.TestName} {arguments}";

            var result = await ProcessUtil.RunAsync(
                "dotnet",
                processArguments,
                workingDirectory: Options.WorkingDirectoryNet,
                log: Options.Debug,
                captureOutput: true,
                captureError: true
            );
        }

        private static void DebugWriteLine(string value)
        {
            if (Options.Debug)
            {
                Console.WriteLine(value);
            }
        }
    }
}
