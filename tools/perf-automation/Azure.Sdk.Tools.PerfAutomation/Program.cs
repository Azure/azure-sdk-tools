using Azure.Sdk.Tools.PerfAutomation.Models;
using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

        private static readonly Dictionary<Language, ILanguage> _languages = new Dictionary<Language, ILanguage>
        {
            { Language.Java, new Java() },
            { Language.JS, new JavaScript() },
            { Language.Net, new Net() },
            { Language.Python, new Python() },
            { Language.Cpp, new Cpp() }
        };

        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            },
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };

        public class Options
        {
            [Option('a', "arguments", HelpText = "Regex of arguments to run")]
            public string Arguments { get; set; }

            [Option('d', "debug")]
            public bool Debug { get; set; }

            [Option('n', "dry-run")]
            public bool DryRun { get; set; }

            [Option("insecure", HelpText = "Allow untrusted SSL certs")]
            public bool Insecure { get; set; }

            [Option('i', "iterations", Default = 1)]
            public int Iterations { get; set; }

            [Option('l', "language", Required = true)]
            public Language Language { get; set; }

            [Option("language-version", Required = true, HelpText = ".NET: 6|7, Java: 8|17, JS: 16|18, Python: 3.10|3.11, Cpp: N/A")]
            public string LanguageVersion { get; set; }

            [Option("no-async")]
            public bool NoAsync { get; set; }

            [Option("no-cleanup", HelpText = "Disables test cleanup")]
            public bool NoCleanup { get; set; }

            [Option("no-sync")]
            public bool NoSync { get; set; }

            [Option('o', "output-file-prefix", Default = "results/results")]
            public string OutputFilePrefix { get; set; }

            [Option('p', "package-versions", HelpText = "Regex of package versions to run")]
            public string PackageVersions { get; set; }

            [Option("profile", HelpText = "Enables capture of profiling data")]
            public bool Profile { get; set; }

            [Option("profilerOpt", HelpText = "Provides additional profiler parameters")]
            public string ProfilerOptions { get; set; }

            [Option("repo-root", Required = true, HelpText = "Path to root of repository in which to run tests")]
            public string RepoRoot { get; set; }

            // TODO: Configure YAML serialization to print URI values
            [Option('x', "test-proxies", Separator = ';', HelpText = "URIs of TestProxy Servers")]
            [YamlMember(typeof(string))]
            public IEnumerable<Uri> TestProxies { get; set; }

            [Option("test-proxy", HelpText = "URI of TestProxy Server")]
            [YamlMember(typeof(string))]
            public Uri TestProxy { get; set; }

            [Option('t', "tests", HelpText = "Regex of tests to run")]
            public string Tests { get; set; }

            [Option("tests-file", Required = true)]
            public string TestsFile { get; set; }
        }

        public static async Task Main(string[] args)
        {
            var parser = new CommandLine.Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = null;
            });

            var parserResult = parser.ParseArguments<Options>(args);

            await parserResult.MapResult(
                (Options options) => Run(options),
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

        private static async Task Run(Options options)
        {
            if (options.Language == Language.JS)
            {
                // JS is async-only
                options.NoSync = true;
            }
            else if (options.Language == Language.Cpp)
            {
                // Cpp is sync-only
                options.NoAsync = true;
            }

            var serviceInfo = DeserializeYaml<ServiceInfo>(options.TestsFile);

            var selectedPackageVersions = serviceInfo.PackageVersions.Where(d =>
                String.IsNullOrEmpty(options.PackageVersions) ||
                Regex.IsMatch(d[serviceInfo.PrimaryPackage], options.PackageVersions, RegexOptions.IgnoreCase));

            var selectedTests = serviceInfo.Tests
                .Where(t =>
                    String.IsNullOrEmpty(options.Tests) ||
                    Regex.IsMatch(t.Test, options.Tests, RegexOptions.IgnoreCase))
                .Select(t => new TestInfo
                {
                    Test = t.Test,
                    Class = t.Class,
                    Arguments = t.Arguments.Where(a =>
                        String.IsNullOrEmpty(options.Arguments) ||
                        Regex.IsMatch(a, options.Arguments, RegexOptions.IgnoreCase))
                })
                .Where(t => t.Arguments.Any());

            var serializer = new Serializer();
            Console.WriteLine("=== Options ===");
            serializer.Serialize(Console.Out, options);

            Console.WriteLine();

            Console.WriteLine("=== Test Plan ===");
            serializer.Serialize(Console.Out, new ServiceInfo()
            {
                Service = serviceInfo.Service,
                Project = serviceInfo.Project,
                PrimaryPackage = serviceInfo.PrimaryPackage,
                PackageVersions = selectedPackageVersions,
                Tests = selectedTests,
            });

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

            var outputFiles = Util.GetUniquePaths(options.OutputFilePrefix, ".json", ".csv", ".txt", ".md");

            // Create output file early so user sees it immediately
            foreach (var outputFile in outputFiles)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                using (File.Create(outputFile)) { }
            }

            var outputJson = outputFiles[0];
            var outputCsv = outputFiles[1];
            var outputTxt = outputFiles[2];
            var outputMd = outputFiles[3];

            var results = new List<Result>();
            DirectoryInfo profileDirectory = null;

            if (options.Profile)
            {
                profileDirectory = Directory.CreateDirectory(Util.GetProfileDirectory(options.RepoRoot));
            }

            foreach (var packageVersions in selectedPackageVersions)
            {
                await RunPackageVersion(
                    options,
                    serviceInfo.Service,
                    serviceInfo.Project,
                    serviceInfo.PrimaryPackage,
                    packageVersions,
                    selectedTests,
                    outputJson,
                    outputCsv,
                    outputTxt,
                    outputMd,
                    results);
            }

            if (options.Profile)
            {
                ZipFile.CreateFromDirectory(profileDirectory.FullName, Path.Combine(profileDirectory.Parent.FullName, $"{options.Language}-{profileDirectory.Name}.zip"));
            }
        }

        private static async Task RunPackageVersion(
            Options options,
            string service,
            string project,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            IEnumerable<TestInfo> tests,
            string outputJson,
            string outputCsv,
            string outputTxt,
            string outputMd,
            List<Result> results)
        {
            var language = options.Language;
            var languageVersion = options.LanguageVersion;

            _languages[language].WorkingDirectory = options.RepoRoot;

            try
            {
                Console.WriteLine($"SetupAsync({project}, {languageVersion}, " +
                    $"{JsonSerializer.Serialize(packageVersions)})");
                Console.WriteLine();

                string setupOutput = null;
                string setupError = null;
                object context = null;
                string setupException = null;

                try
                {
                    (setupOutput, setupError, context) = await _languages[language].SetupAsync(
                        project, languageVersion, primaryPackage, packageVersions, options.Debug);
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
                            Service = service,
                            Test = test.Test,
                            Start = DateTime.Now,
                            Language = language,
                            LanguageVersion = languageVersion,
                            Project = project,
                            LanguageTestName = test.Class,
                            Arguments = allArguments,
                            PrimaryPackage = primaryPackage,
                            PackageVersions = packageVersions,
                            SetupStandardOutput = setupOutput,
                            SetupStandardError = setupError,
                            SetupException = setupException,
                        };

                        results.Add(result);

                        await WriteResults(outputJson, outputCsv, outputTxt, outputMd, results);
                        if (setupException == null)
                        {
                            for (var i = 0; i < options.Iterations; i++)
                            {
                                IterationResult iterationResult;
                                try
                                {
                                    Console.WriteLine($"RunAsync({project}, {languageVersion}, " +
                                        $"{test.Class}, {allArguments}, {context}, {options.Profile}, {options.ProfilerOptions})");
                                    Console.WriteLine();

                                    iterationResult = await _languages[language].RunAsync(
                                        project,
                                        languageVersion,
                                        primaryPackage,
                                        packageVersions,
                                        test.Class,
                                        allArguments,
                                        options.Profile,
                                        options.ProfilerOptions,
                                        context);
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

                                await WriteResults(outputJson, outputCsv, outputTxt, outputMd, results);
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
                    Console.WriteLine($"CleanupAsync({project})");
                    Console.WriteLine();

                    try
                    {
                        await _languages[language].CleanupAsync(project);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        Console.WriteLine();
                    }
                }
            }
        }

        private static async Task WriteResults(string outputJson, string outputCsv, string outputTxt, string outputMd, List<Result> results)
        {
            using (var stream = File.OpenWrite(outputJson))
            {
                await JsonSerializer.SerializeAsync(stream, results, JsonOptions);
            }

            using (var streamWriter = new StreamWriter(outputCsv))
            {
                await WriteResultsSummary(streamWriter, results, OutputFormat.Csv);
            }

            using (var streamWriter = new StreamWriter(outputTxt))
            {
                await WriteResultsSummary(streamWriter, results, OutputFormat.Txt);
            }

            using (var streamWriter = new StreamWriter(outputMd))
            {
                await WriteResultsSummary(streamWriter, results, OutputFormat.Md);
            }
        }

        public static async Task WriteResultsSummary(StreamWriter streamWriter, IEnumerable<Result> results, OutputFormat outputFormat)
        {
            var groups = results.GroupBy(r => (r.Language, r.LanguageVersion, r.Service, r.Test, r.Arguments));

            var resultSummaries = groups.Select(g =>
            {
                var requestedPackageVersions = g.Select(r => r.PackageVersions).Distinct(new DictionaryEqualityComparer<string, string>());

                var runtimePackageVersions = requestedPackageVersions.Select(req =>
                    g.Where(r => r.PackageVersions == req).First().Iterations.FirstOrDefault()?.PackageVersions);

                var resultSummary = new ResultSummary()
                {
                    Language = g.Key.Language,
                    LanguageVersion = g.Key.LanguageVersion,
                    Service = g.Key.Service,
                    Test = g.Key.Test,
                    Arguments = g.Key.Arguments,
                    PrimaryPackage = g.First().PrimaryPackage,
                    RequestedPackageVersions = requestedPackageVersions,
                    RuntimePackageVersions = runtimePackageVersions,
                };

                var operationsPerSecondMax = new List<(string version, double operationsPerSecond)>();
                var operationsPerSecondMean = new List<(string version, double operationsPerSecond)>();

                foreach (var result in g)
                {
                    var primaryPackageVersion = result.PackageVersions?[resultSummary.PrimaryPackage];
                    operationsPerSecondMax.Add((primaryPackageVersion, result.OperationsPerSecondMax));
                    operationsPerSecondMean.Add((primaryPackageVersion, result.OperationsPerSecondMean));
                }

                resultSummary.OperationsPerSecondMax = operationsPerSecondMax;
                resultSummary.OperationsPerSecondMean = operationsPerSecondMean;

                return resultSummary;
            });

            var languageServiceGroups = resultSummaries.GroupBy(r => (r.Language, r.LanguageVersion, r.Service));
            foreach (var group in languageServiceGroups)
            {
                await WriteResultsSummaryThroughput(streamWriter, group, "Max", r => r.OperationsPerSecondMax,
                    r => r.OperationsPerSecondMaxDifferences, outputFormat);

                await WriteResultsSummaryThroughput(streamWriter, group, "Mean", r => r.OperationsPerSecondMean,
                    r => r.OperationsPerSecondMeanDifferences, outputFormat);

                await WriteHeader(streamWriter, "Package Versions", outputFormat);

                var versionHeaders = new string[] { "Name", "Requested", "Runtime" };
                var versionTable = new List<IList<IList<string>>>();

                var primaryPackage = group.First().PrimaryPackage;

                var runtimePackageVersions = group.First().RuntimePackageVersions
                    .Select(p => _languages[group.Key.Language].FilterRuntimePackageVersions(p));

                var packageVersions = group.First().RequestedPackageVersions.Zip(runtimePackageVersions);

                foreach (var (requested, runtime) in packageVersions)
                {
                    // requested is guaranteed to be non-null, runtime may be null

                    var versionRows = new List<IList<string>>();

                    // Primary package first, azure core second, remaining sorted alphabetically
                    var packageNames = requested.Keys.Concat(runtime?.Keys ?? Enumerable.Empty<string>())
                        .Distinct()
                        .OrderBy(n => (n == primaryPackage) ? $"__{n}" :
                            ((n.Contains("core", StringComparison.OrdinalIgnoreCase) &&
                              n.Contains("azure", StringComparison.OrdinalIgnoreCase)) ? $"_{n}" : n));

                    foreach (var packageName in packageNames)
                    {
                        requested.TryGetValue(packageName, out var requestedPackageVersion);

                        string runtimePackageVersion = null;
                        runtime?.TryGetValue(packageName, out runtimePackageVersion);

                        versionRows.Add(new List<string>
                        {
                            packageName,
                            requestedPackageVersion ?? "none",
                            runtimePackageVersion ?? "unknown"
                        });
                    }

                    versionTable.Add(versionRows);
                }

                await streamWriter.WriteLineAsync(TableGenerator.Generate(versionHeaders, versionTable, outputFormat));

                await WriteHeader(streamWriter, "Metadata", outputFormat);
                var metadataHeaders = new string[] { "Name", "Value" };
                var metadataTable = new List<IList<IList<string>>>();
                var metadataRowSets = new List<IList<string>>();
                metadataRowSets.Add(new List<string>(new string[] { "Language", $"{group.Key.Language} ({group.Key.LanguageVersion})" }));
                metadataRowSets.Add(new List<string>(new string[] { "Service", $"{group.Key.Service}" }));
                metadataTable.Add(metadataRowSets);

                await streamWriter.WriteLineAsync(TableGenerator.Generate(metadataHeaders, metadataTable, outputFormat));
            }
        }

        private static async Task WriteHeader(StreamWriter streamWriter, string header, OutputFormat outputFormat)
        {
            await streamWriter.WriteLineAsync($"## {header}");
        }

        private static async Task WriteResultsSummaryThroughput(
            StreamWriter streamWriter,
            IEnumerable<ResultSummary> resultSummaries,
            string aggregateType,
            Func<ResultSummary, IEnumerable<(string version, double operationsPerSecond)>> operationsPerSecond,
            Func<ResultSummary, IEnumerable<double>> operationsPerSecondDifferences,
            OutputFormat outputFormat)
        {
            var versions = operationsPerSecond(resultSummaries.First()).Select(o => o.version);
            var headers = versions.Take(1).Concat(versions.Skip(1).Zip(Enumerable.Repeat("%Change", versions.Count() - 1),
                (f, s) => new[] { f, s }).SelectMany(f => f));

            var testGroups = resultSummaries.GroupBy(g => g.Test);

            await WriteHeader(streamWriter, $"{aggregateType} throughput (ops/sec)", outputFormat);

            headers = headers.Prepend("Arguments").Prepend("Test");

            var table = new List<IList<IList<string>>>();

            foreach (var testGroup in testGroups)
            {
                var rowSet = new List<IList<string>>();
                foreach (var resultSummary in testGroup)
                {
                    var row = new List<string>();
                    row.Add(resultSummary.Test);
                    row.Add(resultSummary.Arguments);

                    var operationsPerSecondStrings = operationsPerSecond(resultSummary)
                        .Select(o => $"{NumberFormatter.Format(o.operationsPerSecond, 4, groupSeparator: outputFormat != OutputFormat.Csv)}");
                    var operationsPerSecondDifferencesStrings = operationsPerSecondDifferences(resultSummary).Select(o => $"{o * 100:N1}%");

                    var values = operationsPerSecondStrings.Take(1).Concat(operationsPerSecondStrings.Skip(1)
                        .Zip(operationsPerSecondDifferencesStrings, (f, s) => new[] { f, s }).SelectMany(f => f));

                    row.AddRange(values);

                    rowSet.Add(row);
                }
                table.Add(rowSet);
            }

            await streamWriter.WriteLineAsync(TableGenerator.Generate(headers.ToList(), table, outputFormat));
        }

        private static T DeserializeYaml<T>(string path)
        {
            using var fileReader = File.OpenText(path);
            var parser = new MergingParser(new YamlDotNet.Core.Parser(fileReader));
            return new Deserializer().Deserialize<T>(parser);
        }
    }
}
