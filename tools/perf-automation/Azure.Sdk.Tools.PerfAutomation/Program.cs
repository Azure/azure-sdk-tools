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

        private static Dictionary<Language, ILanguage> _languages = new Dictionary<Language, ILanguage>
        {
            { Language.Net, new Net() },
            { Language.Java, new Java() },
            { Language.Python, new Python() }
        };

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

            [Option('i', "iterations", Default = 3)]
            public int Iterations { get; set; }

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

            var services = DeserializeYaml<List<ServiceInfo>>(options.InputFile);

            var selectedServices = services.Select(s => new ServiceInfo
            {
                Service = s.Service,
                Languages = s.Languages.Where(l => !options.Languages.Any() || options.Languages.Contains(l.Key))
                    .ToDictionary(p => p.Key, p => p.Value),
                Tests = s.Tests.Where(t =>
                    String.IsNullOrEmpty(options.TestFilter) || Regex.IsMatch(t.Test, options.TestFilter, RegexOptions.IgnoreCase)).Select(t =>
                        new TestInfo
                        {
                            Test = t.Test,
                            Arguments = t.Arguments,
                            TestNames = t.TestNames.Where(n => !options.Languages.Any() || options.Languages.Contains(n.Key))
                                .ToDictionary(p => p.Key, p => p.Value)
                        }
                    )
            });

            Console.WriteLine("=== Test Plan ===");
            var serializer = new Serializer();
            serializer.Serialize(Console.Out, selectedServices);

            if (options.DryRun)
            {
                return;
            }

            var uniqueOutputFile = Util.GetUniquePath(options.OutputFile);
            // Create output file early so user sees it immediately
            using (File.Create(uniqueOutputFile)) { }

            var results = new List<Result>();

            foreach (var service in selectedServices)
            {
                foreach (var l in service.Languages)
                {
                    var language = l.Key;
                    var languageInfo = l.Value;

                    foreach (var packageVersions in languageInfo.PackageVersions)
                    {
                        try
                        {
                            var (setupOutput, setupError, context) = await _languages[language].SetupAsync(languageInfo.Project, packageVersions);

                            foreach (var test in service.Tests)
                            {
                                var syncArguments = test.Arguments.SelectMany(a => new string[] { a, a + " --sync" });

                                foreach (var arguments in syncArguments)
                                {
                                    var allArguments = $"{arguments} {languageInfo.AdditionalArguments}";

                                    for (var i = 0; i < options.Iterations; i++)
                                    {
                                        var result = await _languages[language].RunAsync(
                                            languageInfo.Project,
                                            test.TestNames[language],
                                            allArguments,
                                            context
                                        );

                                        result.TestName = test.Test;

                                        result.Language = language;
                                        result.Project = languageInfo.Project;
                                        result.LanguageTestName = test.TestNames[language];
                                        result.Arguments = allArguments;
                                        result.PackageVersions = packageVersions;
                                        result.SetupStandardOutput = setupOutput;
                                        result.SetupStandardError = setupError;

                                        result.Iteration = i;

                                        results.Add(result);

                                        using var stream = File.OpenWrite(uniqueOutputFile);
                                        await JsonSerializer.SerializeAsync(stream, results, JsonOptions);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            await _languages[language].CleanupAsync(languageInfo.Project);
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
