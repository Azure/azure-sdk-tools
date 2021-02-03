using System;
using System.IO;

namespace Azure.Sdk.Tools.PerfAutomation
{
    static class Util
    {
        public static string GetUniquePath(string path)
        {
            var directoryName = Path.GetDirectoryName(path);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            var uniquePath = Path.Join(directoryName, $"{fileNameWithoutExtension}{extension}");

            int index = 0;
            while (File.Exists(uniquePath))
            {
                index++;
                uniquePath = Path.Join(directoryName, $"{fileNameWithoutExtension}.{index}{extension}");
            }

            using var stream = File.Create(uniquePath);

            return uniquePath;
        }

        public static void DebugWriteLine(string value)
        {
            if (Program.Options.Debug)
            {
                Console.WriteLine(value);
            }
        }
    }
}
