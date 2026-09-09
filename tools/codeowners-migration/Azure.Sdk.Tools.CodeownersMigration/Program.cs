using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;
using Azure.Sdk.Tools.CodeownersMigration.Conversion;
using Azure.Sdk.Tools.CodeownersMigration.Verification;

namespace Azure.Sdk.Tools.CodeownersMigration
{
    internal class Program
    {
        private const int ExitSuccess = 0;
        private const int ExitDifferences = 1;
        private const int ExitError = 2;

        private static int Main(string[] args)
        {
            var rootCommand = new RootCommand("Migration utilities for moving a repository from a hand-maintained CODEOWNERS file to owners.yaml ownership files.");
            int returnCode = ExitError;

            rootCommand.AddCommand(BuildConvertCommand(code => returnCode = code));
            rootCommand.AddCommand(BuildVerifyCommand(code => returnCode = code));

            int parseResult = rootCommand.Invoke(args);
            return parseResult != 0 ? parseResult : returnCode;
        }

        private static Command BuildConvertCommand(Action<int> setReturnCode)
        {
            var codeownersOption = new Option<string>("--codeowners", "Path to the existing CODEOWNERS file.") { IsRequired = true };
            var outputRootOption = new Option<string>("--output-root", "Repository root to write .github/owners.config.yaml and fragment files into.");
            var fragmentSectionOption = new Option<List<string>>("--fragment-section", "Section whose entries are moved into owners.yaml fragments. Repeatable.") { AllowMultipleArgumentsPerToken = true };
            var protectedSectionOption = new Option<List<string>>("--protected-section", "Section to mark 'protected: true'. Repeatable.") { AllowMultipleArgumentsPerToken = true };
            var fragmentGlobOption = new Option<string>("--fragment-glob", () => "sdk/*/owners.yaml", "Where fragment files live. The wildcard segment becomes the fragment directory.");
            var defaultSectionOption = new Option<string>("--default-section", "Value for configs.default-section. Defaults to the first --fragment-section.");
            var teamStorageOption = new Option<string>("--team-storage-uri", "Override the team/user blob storage URI used for team expansion.");
            var dryRunOption = new Option<bool>("--dry-run", "Print the generated files instead of writing them.");

            var command = new Command("convert", "Generate owners.config.yaml and owners.yaml fragments from an existing CODEOWNERS file.")
            {
                codeownersOption, outputRootOption, fragmentSectionOption, protectedSectionOption,
                fragmentGlobOption, defaultSectionOption, teamStorageOption, dryRunOption
            };

            command.SetHandler(context =>
            {
                try
                {
                    List<string> fragmentSections = context.ParseResult.GetValueForOption(fragmentSectionOption) ?? new List<string>();
                    List<string> protectedSections = context.ParseResult.GetValueForOption(protectedSectionOption) ?? new List<string>();
                    string outputRoot = context.ParseResult.GetValueForOption(outputRootOption);
                    bool dryRun = context.ParseResult.GetValueForOption(dryRunOption);

                    if (!dryRun && string.IsNullOrEmpty(outputRoot))
                    {
                        Console.Error.WriteLine("--output-root is required unless --dry-run is specified.");
                        setReturnCode(ExitError);
                        return;
                    }

                    var options = new ConversionOptions
                    {
                        CodeownersPath = context.ParseResult.GetValueForOption(codeownersOption),
                        TeamStorageUri = context.ParseResult.GetValueForOption(teamStorageOption),
                        FragmentGlob = context.ParseResult.GetValueForOption(fragmentGlobOption),
                        FragmentSections = new HashSet<string>(fragmentSections, StringComparer.OrdinalIgnoreCase),
                        ProtectedSections = new HashSet<string>(protectedSections, StringComparer.OrdinalIgnoreCase),
                        DefaultSection = context.ParseResult.GetValueForOption(defaultSectionOption) ?? fragmentSections.FirstOrDefault()
                    };

                    ConversionResult result = new CodeownersConverter(options).Convert();
                    setReturnCode(WriteConversion(result, outputRoot, dryRun));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"convert failed: {ex.Message}");
                    setReturnCode(ExitError);
                }
            });

            return command;
        }

        private static int WriteConversion(ConversionResult result, string outputRoot, bool dryRun)
        {
            foreach (string warning in result.Warnings)
            {
                Console.Error.WriteLine($"warning: {warning}");
            }

            var files = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [".github/owners.config.yaml"] = result.ConfigYaml
            };
            foreach (KeyValuePair<string, string> fragment in result.Fragments)
            {
                files[fragment.Key] = fragment.Value;
            }

            foreach (KeyValuePair<string, string> file in files.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                if (dryRun)
                {
                    Console.WriteLine($"===== {file.Key} =====");
                    Console.WriteLine(file.Value);
                    continue;
                }

                string fullPath = Path.Combine(outputRoot, file.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, file.Value);
                Console.WriteLine($"wrote {file.Key}");
            }

            Console.WriteLine($"{files.Count} file(s), {result.Warnings.Count} warning(s).");
            return ExitSuccess;
        }

        private static Command BuildVerifyCommand(Action<int> setReturnCode)
        {
            var baselineOption = new Option<string>("--baseline", "The pre-existing CODEOWNERS file.") { IsRequired = true };
            var candidateOption = new Option<string>("--candidate", "The newly rendered CODEOWNERS file.") { IsRequired = true };
            var repoRootOption = new Option<string>("--repo-root", "Repository working tree. Directories are enumerated as path resolution targets.");
            var pathsFromOption = new Option<string>("--paths-from", "File containing resolution target paths, one per line.");
            var includeFilesOption = new Option<bool>("--include-files", "Also use files, not just directories, as resolution targets.");
            var sectionOption = new Option<string>("--section", "Compare only this section of both files.");
            var teamStorageOption = new Option<string>("--team-storage-uri", "Override the team/user blob storage URI used for team expansion.");
            var jsonOption = new Option<bool>("--json", "Emit the report as JSON.");

            var command = new Command("verify", "Verify that two CODEOWNERS files are semantically equivalent.")
            {
                baselineOption, candidateOption, repoRootOption, pathsFromOption,
                includeFilesOption, sectionOption, teamStorageOption, jsonOption
            };

            command.SetHandler(context =>
            {
                try
                {
                    string teamStorageUri = context.ParseResult.GetValueForOption(teamStorageOption);
                    string section = context.ParseResult.GetValueForOption(sectionOption);

                    CodeownersDocument baseline = CodeownersDocument.Load(
                        context.ParseResult.GetValueForOption(baselineOption), teamStorageUri, section);
                    CodeownersDocument candidate = CodeownersDocument.Load(
                        context.ParseResult.GetValueForOption(candidateOption), teamStorageUri, section);

                    List<string> targets = CollectResolutionTargets(
                        context.ParseResult.GetValueForOption(repoRootOption),
                        context.ParseResult.GetValueForOption(pathsFromOption),
                        context.ParseResult.GetValueForOption(includeFilesOption));

                    ComparisonReport report = SemanticComparer.Compare(baseline, candidate, targets);
                    bool json = context.ParseResult.GetValueForOption(jsonOption);
                    setReturnCode(WriteReport(report, json));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"verify failed: {ex.Message}");
                    setReturnCode(ExitError);
                }
            });

            return command;
        }

        private static int WriteReport(ComparisonReport report, bool json)
        {
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    equivalent = report.IsEquivalent,
                    pathsCompared = report.PathsCompared,
                    labelSetsCompared = report.LabelSetsCompared,
                    resolutionsCompared = report.ResolutionsCompared,
                    differences = report.Differences.Select(d => new
                    {
                        kind = d.Kind.ToString(),
                        subject = d.Subject,
                        baseline = d.Baseline,
                        candidate = d.Candidate
                    })
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"Paths compared:      {report.PathsCompared}");
                Console.WriteLine($"Label sets compared: {report.LabelSetsCompared}");
                Console.WriteLine($"Paths resolved:      {report.ResolutionsCompared}");
                Console.WriteLine();

                if (report.IsEquivalent)
                {
                    Console.WriteLine("Files are semantically equivalent.");
                }
                else
                {
                    Console.WriteLine($"{report.Differences.Count} difference(s) found:");
                    Console.WriteLine();
                    foreach (Difference difference in report.Differences)
                    {
                        Console.WriteLine(difference.ToString());
                        Console.WriteLine();
                    }
                }
            }

            return report.IsEquivalent ? ExitSuccess : ExitDifferences;
        }

        private static readonly string[] SkippedDirectories = { ".git", ".vs", "node_modules", "bin", "obj", "artifacts", "target", "TestResults" };

        private static List<string> CollectResolutionTargets(string repoRoot, string pathsFrom, bool includeFiles)
        {
            var targets = new List<string>();

            if (!string.IsNullOrEmpty(pathsFrom))
            {
                targets.AddRange(File.ReadAllLines(pathsFrom)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 && !line.StartsWith("#")));
            }

            if (!string.IsNullOrEmpty(repoRoot))
            {
                targets.AddRange(EnumerateRepoPaths(repoRoot, includeFiles));
            }

            return targets;
        }

        private static IEnumerable<string> EnumerateRepoPaths(string repoRoot, bool includeFiles)
        {
            string root = Path.GetFullPath(repoRoot);
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                string current = pending.Pop();

                foreach (string directory in SafeEnumerate(() => Directory.EnumerateDirectories(current)))
                {
                    string name = Path.GetFileName(directory);
                    if (SkippedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    pending.Push(directory);
                    yield return ToRepoRelative(root, directory) + "/";
                }

                if (includeFiles)
                {
                    foreach (string file in SafeEnumerate(() => Directory.EnumerateFiles(current)))
                    {
                        yield return ToRepoRelative(root, file);
                    }
                }
            }
        }

        private static IEnumerable<string> SafeEnumerate(Func<IEnumerable<string>> enumerate)
        {
            try
            {
                return enumerate().ToList();
            }
            catch (UnauthorizedAccessException)
            {
                return Enumerable.Empty<string>();
            }
            catch (IOException)
            {
                return Enumerable.Empty<string>();
            }
        }

        private static string ToRepoRelative(string root, string fullPath)
        {
            return "/" + Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        }
    }
}
