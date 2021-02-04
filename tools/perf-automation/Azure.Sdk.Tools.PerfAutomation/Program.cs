using Azure.Sdk.Tools.PerfAutomation.Models;
using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public static class Program
    {
        public static OptionsDefinition Options { get; set; }
        public static Config Config { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        public class OptionsDefinition
        {
            [Option('c', "configFile", Default = "config.yml")]
            public string ConfigFile { get; set; }

            [Option('d', "debug")]
            public bool Debug { get; set; }

            [Option('n', "dry-run")]
            public bool DryRun { get; set; }

            [Option('l', "languages")]
            public IEnumerable<Language> Languages { get; set; }

            [Option('i', "inputFile", Default = "tests.yml")]
            public string InputFile { get; set; }

            [Option('o', "outputFile", Default = "results.json")]
            public string OutputFile { get; set; }

            [Option('t', "testFilter", HelpText = "Regex of tests to run")]
            public string TestFilter { get; set; }
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

            Config = DeserializeYaml<Config>(options.ConfigFile);

            var tests = DeserializeYaml<List<Test>>(options.InputFile);

            var selectedTests = tests.Where(t =>
                String.IsNullOrEmpty(options.TestFilter) || Regex.IsMatch(t.Name, options.TestFilter, RegexOptions.IgnoreCase));

            foreach (var test in selectedTests)
            {
                test.Languages = new Dictionary<Language, LanguageSettings>(test.Languages.Where(l => !options.Languages.Any() || options.Languages.Contains(l.Key)));
            }

            Console.WriteLine("=== Test Plan ===");
            var serializer = new Serializer();
            serializer.Serialize(Console.Out, selectedTests);

            if (options.DryRun)
            {
                return;
            }

            var uniqueOutputFile = Util.GetUniquePath(options.OutputFile);
            // Create output file early so user sees it immediately
            using (File.Create(uniqueOutputFile)) { }

            var results = new List<Result>();

            foreach (var test in selectedTests)
            {
                foreach (var language in test.Languages)
                {
                    foreach (var arguments in test.Arguments)
                    {
                        foreach (var packageVersions in language.Value.PackageVersions)
                        {
                            Console.WriteLine();

                            Result result = null;

                            switch (language.Key)
                            {
                                case Language.Net:
                                    result = await Net.RunAsync(language.Value, arguments, packageVersions);
                                    break;
                                case Language.Java:
                                    result = await Java.RunAsync(language.Value, arguments, packageVersions);
                                    break;
                                case Language.Python:
                                    result = await Python.RunAsync(language.Value, arguments, packageVersions);
                                    break;
                                default:
                                    continue;
                            }

                            if (result != null)
                            {
                                result.TestName = test.Name;

                                result.Language = language.Key;
                                result.Project = language.Value.Project;
                                result.LanguageTestName = language.Value.TestName;
                                result.Arguments = arguments;
                                result.PackageVersions = packageVersions;
                            }

                            results.Add(result);

                            using var stream = File.OpenWrite(uniqueOutputFile);
                            await JsonSerializer.SerializeAsync(stream, results, JsonOptions);
                        }
                    }
                }
            }
        }

        private static T DeserializeYaml<T>(string path)
        {
            using var fileReader = File.OpenText(path);
            var parser = new MergingParser(new YamlDotNet.Core.Parser(fileReader));
            return new Deserializer().Deserialize<T>(parser);
        }
    }
}
