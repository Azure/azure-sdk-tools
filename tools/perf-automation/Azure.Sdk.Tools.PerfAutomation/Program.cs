using Azure.Sdk.Tools.PerfAutomation.Models;
using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        public const string PackageVersionSource = "source";

        public static OptionsDefinition Options { get; set; }
        public static Config Config { get; set; }

        private static Dictionary<Language, ILanguage> _languages = new Dictionary<Language, ILanguage>
        {
            { Language.Java, new Java() },
            { Language.JS, new JavaScript() },
            { Language.Net, new Net() },
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

            [Option('v', "languageVersions", HelpText = "Regex of language versions to run")]
            public string LanguageVersions { get; set; }

            [Option("input-file", Default = "tests.yml")]
            public string InputFile { get; set; }

            [Option("no-async")]
            public bool NoAsync { get; set; }

            [Option("no-sync")]
            public bool NoSync { get; set; }

            [Option('o', "output-file", Default = "results.json")]
            public string OutputFile { get; set; }

            [Option('p', "packageVersions", HelpText = "Regex of package versions to run")]
            public string PackageVersions { get; set; }

            [Option('s', "services", HelpText = "Regex of services to run")]
            public string Services { get; set; }

            [Option('t', "tests", HelpText = "Regex of tests to run")]
            public string Tests { get; set; }
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

            var input = DeserializeYaml<Input>(options.InputFile);

            var selectedlanguages = input.Languages
                .Where(l => !options.Languages.Any() || options.Languages.Contains(l.Key))
                .ToDictionary(l => l.Key, l => new LanguageInfo()
                {
                    DefaultVersions = l.Value.DefaultVersions.Where(v =>
                            (String.IsNullOrEmpty(options.LanguageVersions) || Regex.IsMatch(v, options.LanguageVersions)) &&
                            !(l.Key == Language.Net && v == "net461" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))),
                    OptionalVersions = l.Value.OptionalVersions.Where(v =>
                            (!String.IsNullOrEmpty(options.LanguageVersions) && Regex.IsMatch(v, options.LanguageVersions)) &&
                            !(l.Key == Language.Net && v == "net461" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)))
                });


            var selectedServices = input.Services
                .Where(s => String.IsNullOrEmpty(options.Services) || Regex.IsMatch(s.Service, options.Services, RegexOptions.IgnoreCase))
                .Select(s => new ServiceInfo
                {
                    Service = s.Service,
                    Languages = s.Languages.Where(l => !options.Languages.Any() || options.Languages.Contains(l.Key))
                    .ToDictionary(p => p.Key, p => new ServiceLanguageInfo()
                    {
                        Project = p.Value.Project,
                        AdditionalArguments = p.Value.AdditionalArguments,
                        PackageVersions = p.Value.PackageVersions.Where(d => d.Keys.Concat(d.Values).Any(s =>
                            String.IsNullOrEmpty(options.PackageVersions) || Regex.IsMatch(s, options.PackageVersions)
                        ))
                    }),
                    Tests = s.Tests.Where(t =>
                        String.IsNullOrEmpty(options.Tests) || Regex.IsMatch(t.Test, options.Tests, RegexOptions.IgnoreCase)).Select(t =>
                            new TestInfo
                            {
                                Test = t.Test,
                                Arguments = t.Arguments,
                                TestNames = t.TestNames.Where(n => !options.Languages.Any() || options.Languages.Contains(n.Key))
                                    .ToDictionary(p => p.Key, p => p.Value)
                            }
                    )
                })
                .Where(s => s.Tests.Any());

            var serializer = new Serializer();
            Console.WriteLine("=== Options ===");
            serializer.Serialize(Console.Out, options);

            Console.WriteLine();

            Console.WriteLine("=== Test Plan ===");
            serializer.Serialize(Console.Out, new Input() { Languages = selectedlanguages, Services = selectedServices });

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
                    var serviceLanugageInfo = l.Value;

                    var languageInfo = selectedlanguages[language];

                    foreach (var languageVersion in languageInfo.DefaultVersions.Concat(languageInfo.OptionalVersions))
                    {

                        foreach (var packageVersions in serviceLanugageInfo.PackageVersions)
                        {
                            try
                            {
                                // TODO: Handle exception thrown by setup.  Write empty result for all tests.
                                var (setupOutput, setupError, context) = await _languages[language].SetupAsync(
                                    serviceLanugageInfo.Project, languageVersion, packageVersions);

                                foreach (var test in service.Tests)
                                {
                                    IEnumerable<string> selectedArguments;
                                    if (!options.NoAsync && !options.NoSync)
                                    {
                                        selectedArguments = test.Arguments.SelectMany(a => new string[] { a, a + " --sync" });
                                    }
                                    else if (!options.NoSync)
                                    {
                                        selectedArguments = test.Arguments.Select(a => a + " --sync");
                                    }
                                    else if (!options.NoAsync)
                                    {
                                        selectedArguments = test.Arguments;
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Cannot set both --no-sync and --no-async");
                                    }

                                    foreach (var arguments in selectedArguments)
                                    {
                                        var allArguments = $"{arguments} {serviceLanugageInfo.AdditionalArguments}";

                                        var result = new Result
                                        {
                                            TestName = test.Test,
                                            Start = DateTime.Now,
                                            Language = language,
                                            LanguageVersion = languageVersion,
                                            Project = serviceLanugageInfo.Project,
                                            LanguageTestName = test.TestNames[language],
                                            Arguments = allArguments,
                                            PackageVersions = packageVersions,
                                            SetupStandardOutput = setupOutput,
                                            SetupStandardError = setupError,
                                        };

                                        results.Add(result);

                                        using (var stream = File.OpenWrite(uniqueOutputFile))
                                        {
                                            await JsonSerializer.SerializeAsync(stream, results, JsonOptions);
                                        }

                                        for (var i = 0; i < options.Iterations; i++)
                                        {
                                            IterationResult iterationResult;
                                            try
                                            {
                                                iterationResult = await _languages[language].RunAsync(
                                                    serviceLanugageInfo.Project,
                                                    languageVersion,
                                                    test.TestNames[language],
                                                    allArguments,
                                                    context
                                                );
                                            }
                                            catch (Exception e)
                                            {
                                                iterationResult = new IterationResult
                                                {
                                                    OperationsPerSecond = double.MinValue,
                                                    StandardError = e.ToString()
                                                };
                                            }

                                            // Replace non-finite values with minvalue, since non-finite values
                                            // are not JSON serializable
                                            if (!double.IsFinite(iterationResult.OperationsPerSecond))
                                            {
                                                iterationResult.OperationsPerSecond = double.MinValue;
                                            }

                                            result.Iterations.Add(iterationResult);

                                            using (var stream = File.OpenWrite(uniqueOutputFile))
                                            {
                                                await JsonSerializer.SerializeAsync(stream, results, JsonOptions);
                                            }
                                        }

                                        result.End = DateTime.Now;
                                    }
                                }
                            }
                            finally
                            {
                                await _languages[language].CleanupAsync(serviceLanugageInfo.Project);
                            }
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
