using CommandLine;
using CommandLine.Text;
using System;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public static class Program
    {
        private class Options
        {
            [Option('l', "language", Required = true)]
            public Language Language { get; set; }
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
            Console.WriteLine("hello");
        }
    }
}
