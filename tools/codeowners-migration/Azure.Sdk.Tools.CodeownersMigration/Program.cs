using System;
using System.Collections.Generic;
using System.CommandLine;
using Azure.Sdk.Tools.CodeownersMigration.Verification;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersMigration
{
    internal class Program
    {
        private const int ExitEquivalent = 0;
        private const int ExitDifferences = 1;
        private const int ExitError = 2;

        private static int Main(string[] args)
        {
            var baseOption = new Option<string>("--base", "Path to the base CODEOWNERS file.") { IsRequired = true };
            var compareOption = new Option<string>("--compare", "Path to the CODEOWNERS file to compare against the base.") { IsRequired = true };
            var teamStorageOption = new Option<string>("--team-storage-uri", "Override the team/user blob storage URI used for team expansion.");
            var maxDifferencesOption = new Option<int>("--max-differences", () => 50, "Maximum number of differences to print. Use 0 for no limit.");

            var rootCommand = new RootCommand(
                "Compares two CODEOWNERS files for semantic equivalence. Exits 0 when they are equivalent, " +
                "1 when they differ, and 2 when the comparison could not be performed.")
            {
                baseOption, compareOption, teamStorageOption, maxDifferencesOption
            };

            int returnCode = ExitError;
            rootCommand.SetHandler(context =>
            {
                returnCode = Run(
                    context.ParseResult.GetValueForOption(baseOption),
                    context.ParseResult.GetValueForOption(compareOption),
                    context.ParseResult.GetValueForOption(teamStorageOption),
                    context.ParseResult.GetValueForOption(maxDifferencesOption));
            });

            int parseResult = rootCommand.Invoke(args);
            return parseResult != 0 ? parseResult : returnCode;
        }

        private static int Run(string basePath, string comparePath, string teamStorageUri, int maxDifferences)
        {
            List<CodeownersEntry> baseEntries;
            List<CodeownersEntry> compareEntries;

            try
            {
                baseEntries = CodeownersFileLoader.Load(basePath, teamStorageUri);
                compareEntries = CodeownersFileLoader.Load(comparePath, teamStorageUri);
            }
            catch (CodeownersLoadException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitError;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load the CODEOWNERS files: {ex.Message}");
                return ExitError;
            }

            Console.WriteLine($"base:    {basePath} ({baseEntries.Count} entries)");
            Console.WriteLine($"compare: {comparePath} ({compareEntries.Count} entries)");

            List<EntryDifference> differences = new EntryComparer().Compare(baseEntries, compareEntries);

            if (differences.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("The two files are semantically equivalent: every entry matches, in the same order.");
                return ExitEquivalent;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine($"Found {differences.Count} difference(s):");
            Console.Error.WriteLine();

            int printed = 0;
            foreach (EntryDifference difference in differences)
            {
                if (maxDifferences > 0 && printed == maxDifferences)
                {
                    Console.Error.WriteLine($"... and {differences.Count - printed} more. Re-run with --max-differences 0 to see all of them.");
                    break;
                }
                Console.Error.WriteLine(difference.ToString());
                printed++;
            }

            return ExitDifferences;
        }
    }
}
