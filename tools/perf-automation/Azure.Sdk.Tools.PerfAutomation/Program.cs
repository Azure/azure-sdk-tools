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

            parserResult.WithParsed(options => Options = options);

            await parserResult.MapResult(
                options => Run(),
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

        private static async Task Run()
        {
            Config = DeserializeYaml<Config>(Options.ConfigFile);

            var input = DeserializeYaml<Input>(Options.InputFile);

            var selectedlanguages = input.Languages
                .Where(l => !Options.Languages.Any() || Options.Languages.Contains(l.Key))
                .ToDictionary(l => l.Key, l => new LanguageInfo()
                {
                    DefaultVersions = l.Value.DefaultVersions.Where(v =>
                            (String.IsNullOrEmpty(Options.LanguageVersions) || Regex.IsMatch(v, Options.LanguageVersions)) &&
                            !(l.Key == Language.Net && v == "net461" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))),
                    OptionalVersions = l.Value.OptionalVersions.Where(v =>
                            (!String.IsNullOrEmpty(Options.LanguageVersions) && Regex.IsMatch(v, Options.LanguageVersions)) &&
                            !(l.Key == Language.Net && v == "net461" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)))
                });


            var selectedServices = input.Services
                .Where(s => String.IsNullOrEmpty(Options.Services) || Regex.IsMatch(s.Service, Options.Services, RegexOptions.IgnoreCase))
                .Select(s => new ServiceInfo
                {
                    Service = s.Service,
                    Languages = s.Languages
                        .Where(l => !Options.Languages.Any() || Options.Languages.Contains(l.Key))
                        .ToDictionary(p => p.Key, p => new ServiceLanguageInfo()
                        {
                            Project = p.Value.Project,
                            AdditionalArguments = p.Value.AdditionalArguments,
                            PackageVersions = p.Value.PackageVersions.Where(d => d.Keys.Concat(d.Values).Any(s =>
                                String.IsNullOrEmpty(Options.PackageVersions) || Regex.IsMatch(s, Options.PackageVersions)
                            ))
                        }),
                    Tests = s.Tests
                        .Where(t => String.IsNullOrEmpty(Options.Tests) || Regex.IsMatch(t.Test, Options.Tests, RegexOptions.IgnoreCase))
                        .Select(t => new TestInfo
                        {
                            Test = t.Test,
                            Arguments = t.Arguments,
                            TestNames = t.TestNames.Where(n => !Options.Languages.Any() || Options.Languages.Contains(n.Key))
                                        .ToDictionary(p => p.Key, p => p.Value)
                        })
                        .Where(t => t.TestNames.Any()),
                })
                .Where(s => s.Tests.Any());

            var serializer = new Serializer();
            Console.WriteLine("=== Options ===");
            serializer.Serialize(Console.Out, Options);

            Console.WriteLine();

            Console.WriteLine("=== Test Plan ===");
            serializer.Serialize(Console.Out, new Input() { Languages = selectedlanguages, Services = selectedServices });

            if (Options.DryRun)
            {
                Console.WriteLine();
                Console.Write("Press 'y' to continue, or any other key to exit: ");
                var key = Console.ReadKey();
                Console.WriteLine();
                Console.WriteLine();
                if (char.ToLowerInvariant(key.KeyChar) != 'y')
                {
                    return;
                }
            }

            var outputFile = Util.GetUniquePath(Options.OutputFile);
            // Create output file early so user sees it immediately
            using (File.Create(outputFile)) { }

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
                            await RunPackageVersion(outputFile, results, service.Tests,
                                language, serviceLanugageInfo, languageVersion, packageVersions);
                        }
                    }
                }
            }
        }

        private static async Task RunPackageVersion(string outputFile, List<Result> results, IEnumerable<TestInfo> tests,
            Language language, ServiceLanguageInfo serviceLanguageInfo, string languageVersion, IDictionary<string, string> packageVersions)
        {
            try
            {
                Console.WriteLine($"SetupAsync({serviceLanguageInfo.Project}, {languageVersion}, " +
                    $"{JsonSerializer.Serialize(packageVersions)})");
                Console.WriteLine();

                string setupOutput = null;
                string setupError = null;
                string context = null;
                string setupException = null;

                try
                {
                    (setupOutput, setupError, context) = await _languages[language].SetupAsync(
                        serviceLanguageInfo.Project, languageVersion, packageVersions);
                }
                catch (Exception e)
                {
                    setupException = e.ToString();

                    Console.WriteLine(e);
                    Console.WriteLine();
                }

                foreach (var test in tests)
                {
                    IEnumerable<string> selectedArguments;
                    if (!Options.NoAsync && !Options.NoSync)
                    {
                        selectedArguments = test.Arguments.SelectMany(a => new string[] { a, a + " --sync" });
                    }
                    else if (!Options.NoSync)
                    {
                        selectedArguments = test.Arguments.Select(a => a + " --sync");
                    }
                    else if (!Options.NoAsync)
                    {
                        selectedArguments = test.Arguments;
                    }
                    else
                    {
                        throw new InvalidOperationException("Cannot set both --no-sync and --no-async");
                    }

                    foreach (var arguments in selectedArguments)
                    {
                        var allArguments = $"{arguments} {serviceLanguageInfo.AdditionalArguments}";

                        var result = new Result
                        {
                            TestName = test.Test,
                            Start = DateTime.Now,
                            Language = language,
                            LanguageVersion = languageVersion,
                            Project = serviceLanguageInfo.Project,
                            LanguageTestName = test.TestNames[language],
                            Arguments = allArguments,
                            PackageVersions = packageVersions,
                            SetupStandardOutput = setupOutput,
                            SetupStandardError = setupError,
                            SetupException = setupException,
                        };

                        results.Add(result);

                        using (var stream = File.OpenWrite(outputFile))
                        {
                            await JsonSerializer.SerializeAsync(stream, results, JsonOptions);
                        }
                        if (setupException == null)
                        {
                            for (var i = 0; i < Options.Iterations; i++)
                            {
                                IterationResult iterationResult;
                                try
                                {
                                    Console.WriteLine($"RunAsync({serviceLanguageInfo.Project}, {languageVersion}, " +
                                        $"{test.TestNames[language]}, {allArguments}, {context})");
                                    Console.WriteLine();

                                    iterationResult = await _languages[language].RunAsync(
                                        serviceLanguageInfo.Project,
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
                                        Exception = e.ToString(),
                                    };

                                    Console.WriteLine(e);
                                    Console.WriteLine();
                                }

                                // Replace non-finite values with minvalue, since non-finite values
                                // are not JSON serializable
                                if (!double.IsFinite(iterationResult.OperationsPerSecond))
                                {
                                    iterationResult.OperationsPerSecond = double.MinValue;
                                }

                                result.Iterations.Add(iterationResult);

                                using (var stream = File.OpenWrite(outputFile))
                                {
                                    await JsonSerializer.SerializeAsync(stream, results, JsonOptions);
                                }
                            }
                        }

                        result.End = DateTime.Now;
                    }
                }
            }
            finally
            {
                Console.WriteLine($"CleanupAsync({serviceLanguageInfo.Project})");
                Console.WriteLine();

                try
                {
                    await _languages[language].CleanupAsync(serviceLanguageInfo.Project);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Console.WriteLine();
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
