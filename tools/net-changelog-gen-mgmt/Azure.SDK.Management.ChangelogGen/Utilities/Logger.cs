// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.SDK.ChangelogGen.Utilities
{
    internal static class Logger
    {
        public static void Log(string message, params object[] args)
        {
            OutputInColor(ConsoleColor.White, message, args);
        }

        public static void Error(string message, params object[] args)
        {
            OutputInColor(ConsoleColor.Red, message, args);
        }

        public static void Warning(string message, params object[] args)
        {
            OutputInColor(ConsoleColor.Yellow, message, args);
        }

        public static void Verbose(string message, params object[] args)
        {
            OutputInColor(ConsoleColor.Gray, message, args);
        }

        private static void OutputInColor(ConsoleColor color, string message, params object[] args)
        {
            ConsoleColor c = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                if (args != null && args.Length > 0)
                    Console.WriteLine(message, args);
                else
                    Console.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = c;
            }
        }
    }
}
