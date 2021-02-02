using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public static class Program
    {
        private class Options
        {
            [Option('d', "directory", Required = true)]
            public DirectoryInfo Directory { get; set; }

            [Option('l', "language", Required = true)]
            public Language Language { get; set; }

            [Option('p', "packageReferences", HelpText = "List of package references to override.\nFormat: <name>@<version> ...\nExample: foo@1.0.0 bar@2.0.0")]
            public IEnumerable<PackageReference> PackageReferences { get; set; }
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
            Console.WriteLine($"Directory: {options.Directory.FullName}");
            Console.WriteLine($"Language: {options.Language}");

            foreach (var p in options.PackageReferences)
            {
                Console.WriteLine($"PackageReference: {p.Name}@{p.Version}");
            }
        }
    }
}
