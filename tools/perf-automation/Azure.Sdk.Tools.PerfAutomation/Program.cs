using Azure.Sdk.Tools.PerfAutomation.Models;
using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        }

        public static void Main(string[] args)
        {
            var parser = new CommandLine.Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = null;
            });

            var parserResult = parser.ParseArguments<OptionsDefinition>(args);

            parserResult
                .WithParsed(options => Run(options))
                .WithNotParsed(errors => DisplayHelp(parserResult));
        }

        static void DisplayHelp<T>(ParserResult<T> result)
        {
            var helpText = HelpText.AutoBuild(result, settings =>
            {
                settings.AddEnumValuesToHelpText = true;
                return settings;
            });

            Console.Error.WriteLine(helpText);
        }

        private static void Run(OptionsDefinition options)
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
                                    RunNet(language.Value, arguments, packageVersions);
                                    break;
                                default:
                                    continue;
                            }
                        }
                    }

                }
            }
        }

        private static void RunNet(LanguageSettings languageSettings, string arguments, IDictionary<string, string> packageVersions)
        {
            DebugWriteLine("RunNet");

            //using var process = new Process();

            //var arguments = $"run -c release -f netcoreapp2.1 -- {options.Test} -w 0 -d 1 {options.Arguments}";
            //if (options.Parallel.HasValue)
            //{
            //    arguments = $"{arguments} -p {options.Parallel}";
            //}

            //var startInfo = new ProcessStartInfo("dotnet", arguments);
            //startInfo.UseShellExecute = false;
            //startInfo.RedirectStandardOutput = true;
            //startInfo.RedirectStandardError = true;

            //startInfo.WorkingDirectory = options.Directory.FullName;

            //process.StartInfo = startInfo;
            //process.Start();

            //process.WaitForExit();

            //var output = process.StandardOutput.ReadToEnd();
            //var error = process.StandardError.ReadToEnd();

            //Console.WriteLine($"=== Output ===\n{output}\n\n=== Error ===\n{error}");
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
