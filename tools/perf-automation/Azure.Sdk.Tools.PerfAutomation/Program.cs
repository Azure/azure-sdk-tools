using Azure.Sdk.Tools.PerfAutomation.Models;
using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public static class Program
    {
        private class Options
        {
            [Option('i', "inputFile", Default = "input.yml")]
            public string InputFile { get; set; }

            [Option('o', "outputFile", Default = "output.csv")]
            public string OutputFile { get; set; }
        }

        public static void Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = null;
            });

            var parserResult = parser.ParseArguments<Options>(args);

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

        private static void Run(Options options)
        {
            Console.WriteLine($"InputFile: {options.InputFile}");
            Console.WriteLine($"OutputFile: {options.OutputFile}");

            var input = File.ReadAllText(options.InputFile);

            var deserializer = new Deserializer();
            var tests = deserializer.Deserialize<List<Test>>(input);
            Console.WriteLine(tests);
        }

        private static void RunNet(Options options)
        {
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
    }
}
