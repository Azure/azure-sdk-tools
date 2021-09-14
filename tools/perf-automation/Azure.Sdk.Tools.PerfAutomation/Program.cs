using Azure.Sdk.Tools.PerfAutomation.Models;
using CommandLine;
using CommandLine.Text;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
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

        public static Config Config { get; set; }

        private static Dictionary<Language, ILanguage> _languages = new Dictionary<Language, ILanguage>
        {
            { Language.Java, new Java() },
            { Language.JS, new JavaScript() },
            { Language.Net, new Net() },
            { Language.Python, new Python() },
            { Language.Cpp, new Cpp() }
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            },
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };

        public class CommonOptions
        {
            [Option("input-file", Default = "tests.yml")]
            public string InputFile { get; set; }
        }

        [Verb("run", HelpText = "Run perf tests and collect results")]
        public class RunOptions : CommonOptions
        {
            [Option('a', "arguments", HelpText = "Regex of arguments to run")]
            public string Arguments { get; set; }

            [Option('c', "configFile", Default = "config.yml")]
            public string ConfigFile { get; set; }

            [Option('d', "debug")]
            public bool Debug { get; set; }

            [Option('n', "dry-run")]
            public bool DryRun { get; set; }

            [Option("insecure", HelpText = "Allow untrusted SSL certs")]
            public bool Insecure { get; set; }

            [Option('i', "iterations", Default = 1)]
            public int Iterations { get; set; }

            [Option('l', "languages")]
            public IEnumerable<Language> Languages { get; set; }

            [Option('v', "languageVersions", HelpText = "Regex of language versions to run")]
            public string LanguageVersions { get; set; }

            [Option("no-async")]
            public bool NoAsync { get; set; }

            [Option("no-cleanup", HelpText = "Disables test cleanup")]
            public bool NoCleanup { get; set; }

            [Option("no-sync")]
            public bool NoSync { get; set; }

            [Option('o', "output-file", Default = "results/results.json")]
            public string OutputFile { get; set; }

            [Option('p', "packageVersions", HelpText = "Regex of package versions to run")]
            public string PackageVersions { get; set; }

            [Option('s', "services", HelpText = "Regex of services to run")]
            public string Services { get; set; }

            // TODO: Configure YAML serialization to print URI values
            [Option('x', "test-proxies", Separator = ';', HelpText = "URIs of TestProxy Servers")]
            [YamlMember(typeof(string))]
            public IEnumerable<Uri> TestProxies { get; set; }

            [Option("test-proxy", HelpText = "URI of TestProxy Server")]
            [YamlMember(typeof(string))]
            public Uri TestProxy { get; set; }

            [Option('t', "tests", HelpText = "Regex of tests to run")]
            public string Tests { get; set; }
        }

        [Verb("update", HelpText = "Update language and package versions")]
        public class UpdateOptions : CommonOptions
        {
        }

        [Verb("csv", HelpText = "Generate CSV from results.json files")]
        public class CsvOptions
        {
            [Option('i', "input-folder", Default = "results")]
            public string InputFolder { get; set; }

            [Option('o', "output-file", Default = "results/results.csv")]
            public string OutputFile { get; set; }
        }

        public static async Task Main(string[] args)
        {
            var parser = new CommandLine.Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = null;
            });

            var parserResult = parser.ParseArguments<RunOptions, UpdateOptions, CsvOptions>(args);

            await parserResult.MapResult(
                (RunOptions options) => Run(options),
                (UpdateOptions options) => Update(options),
                (CsvOptions options) => Csv(options),
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

        private static async Task Run(RunOptions options)
        {
            Config = DeserializeYaml<Config>(options.ConfigFile);

            var input = DeserializeYaml<Input>(options.InputFile);

            var selectedlanguages = input.Languages
                .Where(l => !options.Languages.Any() || options.Languages.Contains(l.Key))
                .ToDictionary(l => l.Key, l => new LanguageInfo()
                {
                    DefaultVersions = l.Value.DefaultVersions.Where(v =>
                            (String.IsNullOrEmpty(options.LanguageVersions) || Regex.IsMatch(v, options.LanguageVersions)) &&
                            !(l.Key == Language.Net && v == "net461" && !Util.IsWindows)),
                    OptionalVersions = l.Value.OptionalVersions.Where(v =>
                            (!String.IsNullOrEmpty(options.LanguageVersions) && Regex.IsMatch(v, options.LanguageVersions)) &&
                            !(l.Key == Language.Net && v == "net461" && !Util.IsWindows))
                });


            var selectedServices = input.Services
                .Where(s => String.IsNullOrEmpty(options.Services) || Regex.IsMatch(s.Service, options.Services, RegexOptions.IgnoreCase))
                .Select(s => new ServiceInfo
                {
                    Service = s.Service,
                    Languages = s.Languages
                        .Where(l => !options.Languages.Any() || options.Languages.Contains(l.Key))
                        .ToDictionary(p => p.Key, p => new ServiceLanguageInfo()
                        {
                            Project = p.Value.Project,
                            AdditionalArguments = p.Value.AdditionalArguments,
                            PackageVersions = p.Value.PackageVersions.Where(d => d.Keys.Concat(d.Values).Any(s =>
                                String.IsNullOrEmpty(options.PackageVersions) || Regex.IsMatch(s, options.PackageVersions)
                            ))
                        }),
                    Tests = s.Tests
                        .Where(t => String.IsNullOrEmpty(options.Tests) || Regex.IsMatch(t.Test, options.Tests, RegexOptions.IgnoreCase))
                        .Select(t => new TestInfo
                        {
                            Test = t.Test,
                            Arguments = t.Arguments.Where(a =>
                                String.IsNullOrEmpty(options.Arguments) || Regex.IsMatch(a, options.Arguments, RegexOptions.IgnoreCase)),
                            TestNames = t.TestNames.Where(n => !options.Languages.Any() || options.Languages.Contains(n.Key))
                                        .ToDictionary(p => p.Key, p => p.Value)
                        })
                        .Where(t => t.TestNames.Any())
                        .Where(t => t.Arguments.Any()),
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

            var outputFile = Util.GetUniquePath(options.OutputFile);
            // Create output file early so user sees it immediately
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
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
                            await RunPackageVersion(options, outputFile, results, service,
                                language, serviceLanugageInfo, languageVersion, packageVersions);
                        }
                    }
                }
            }
        }

        private static async Task RunPackageVersion(RunOptions options, string outputFile, List<Result> results, ServiceInfo service,
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
                        var allArguments = arguments;

                        if (serviceLanguageInfo.AdditionalArguments != null)
                        {
                            foreach (var kvp in serviceLanguageInfo.AdditionalArguments)
                            {
                                var (name, value) = (kvp.Key, kvp.Value);

                                if (!arguments.Contains($"--{name} "))
                                {
                                    allArguments += $" --{name} {value}";
                                }
                            }
                        }

                        if (options.Insecure)
                        {
                            allArguments += " --insecure";
                        }

                        if (options.TestProxies != null && options.TestProxies.Any())
                        {
                            allArguments += $" --test-proxies {String.Join(';', options.TestProxies)}";
                        }

                        if (options.TestProxy != null)
                        {
                            allArguments += $" --test-proxy {options.TestProxy}";
                        }

                        var result = new Result
                        {
                            Service = service.Service,
                            Test = test.Test,
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
                            for (var i = 0; i < options.Iterations; i++)
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
                                        packageVersions,
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
                if (!options.NoCleanup)
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
        }

        private static async Task Update(UpdateOptions options)
        {
            await Task.CompletedTask;
        }

        private static async Task Csv(CsvOptions options)
        {
            var serializer = new Serializer();
            Console.WriteLine("=== Options ===");
            serializer.Serialize(Console.Out, options);
            Console.WriteLine();

            var csvResults = new List<CsvResult>();

            foreach (var inputFile in Directory.EnumerateFiles(options.InputFolder, "*.json", SearchOption.AllDirectories))
            {
                Console.WriteLine(inputFile);

                List<Result> results;
                using (var stream = File.OpenRead(inputFile))
                {
                    results = await JsonSerializer.DeserializeAsync<List<Result>>(stream, JsonOptions);
                }

                foreach (var result in results)
                {
                    var csvResult = new CsvResult
                    {
                        Service = result.Service,
                        Test = result.Test,
                        Language = result.Language,
                        OperationsPerSecondMax = result.OperationsPerSecondMax
                    };

                    csvResult.PackageVersions =
                        String.Join(",", result.PackageVersions.Select(kvp => String.Join(":", kvp.Key, kvp.Value)));

                    var sizeMatch = Regex.Match(result.Arguments, @"--size\s+(\d+)");
                    if (sizeMatch.Success)
                    {
                        csvResult.Size = long.Parse(sizeMatch.Groups[1].Value);
                    }

                    var countMatch = Regex.Match(result.Arguments, @"--count\s+(\d+)");
                    if (countMatch.Success)
                    {
                        csvResult.Count = int.Parse(countMatch.Groups[1].Value);
                    }

                    var parallelMatch = Regex.Match(result.Arguments, @"--parallel\s+(\d+)");
                    if (parallelMatch.Success)
                    {
                        csvResult.Parallel = int.Parse(parallelMatch.Groups[1].Value);
                    }

                    csvResults.Add(csvResult);
                }
            }

            var outputFile = Util.GetUniquePath(options.OutputFile);

            using (var consoleWriter = new CsvWriter(Console.Out, CultureInfo.InvariantCulture))
            {
                await consoleWriter.WriteRecordsAsync(csvResults);
            }

            using (var outputFileWriter = new CsvWriter(new StreamWriter(outputFile), CultureInfo.InvariantCulture))
            {
                await outputFileWriter.WriteRecordsAsync(csvResults);
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
