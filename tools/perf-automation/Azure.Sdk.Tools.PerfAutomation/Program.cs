using CommandLine;
using System;

namespace PerfAutomation
{
    public static class Program
    {
        private class Options
        {
        }

        public static void Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.HelpWriter = Console.Error;
            });

            parser.ParseArguments<Options>(args).WithParsed(options => Run(options));
        }

        private static void Run(Options options)
        {
            Console.WriteLine("hello");
        }
    }
}
