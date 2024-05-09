using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Console;
using Microsoft.Build.Tasks;
using Microsoft.Security.Utilities;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class SecretScanner
    {
        public SecretMasker SecretMasker = new SecretMasker(
            WellKnownRegexPatterns.HighConfidenceMicrosoftSecurityModels.Concat(WellKnownRegexPatterns.LowConfidencePotentialSecurityKeys),
            generateCorrelatingIds: true);

        private IConsoleWrapper Console;

        public SecretScanner(IConsoleWrapper consoleWrapper)
        {
            Console = consoleWrapper;
        }

        public async Task<List<Tuple<string, Detection>>> DiscoverSecrets(string assetRepoRoot, IEnumerable<string> relativePaths)
        {
            var detectedSecrets = new List<Tuple<string, Detection>>();
            var total = relativePaths.Count();
            var seen = 0;
            Console.WriteLine(string.Empty);
            foreach (string filePath in relativePaths)
            {
                var content = await ReadFile(Path.Combine(assetRepoRoot, filePath));
                var fileDetections = DetectSecrets(content);

                if (fileDetections != null && fileDetections.Count > 0)
                {
                    foreach (Detection detection in fileDetections)
                    {
                        detectedSecrets.Add(Tuple.Create(filePath, detection));
                    }
                }
                seen++;
                System.Console.Write($"\r\u001b[2KScanned {seen}/{total}.");
            }
            Console.WriteLine(string.Empty);

            return detectedSecrets;
        }

        public async Task<List<Tuple<string, Detection>>> DiscoverSecrets(string inputDirectory)
        {
            var files = Directory.GetFiles(inputDirectory, "*", SearchOption.AllDirectories);
            var detectedSecrets = new List<Tuple<string, Detection>>();

            var seen = 0;
            Console.WriteLine(string.Empty);
            foreach (string filePath in files)
            {
                var content = await ReadFile(filePath);
                var fileDetections = DetectSecrets(content);

                if (fileDetections != null && fileDetections.Count > 0)
                {
                    foreach(Detection detection in fileDetections)
                    {
                        detectedSecrets.Add(Tuple.Create(filePath, detection));
                    }
                }
                seen++;
                System.Console.Write($"\r\u001b[2KScanned {seen}/{files.Length}.");
            }

            Console.WriteLine(string.Empty);

            return detectedSecrets;
        }

        private async Task<string> ReadFile(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private ICollection<Detection> DetectSecrets(string stringContent)
        {
            return SecretMasker.DetectSecrets(stringContent);
        }
        
    }
}
