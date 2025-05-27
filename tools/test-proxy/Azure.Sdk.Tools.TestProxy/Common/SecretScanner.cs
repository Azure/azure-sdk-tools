
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Console;
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

        public List<Tuple<string, Detection>> DiscoverSecrets(string assetRepoRoot, IEnumerable<string> relativePaths)
        {
            var detectedSecrets = new ConcurrentBag<Tuple<string, Detection>>();
            var total = relativePaths.Count();
            var seen = 0;

            if (relativePaths.Count() > 0)
            {
                Console.WriteLine(string.Empty);

                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                Parallel.ForEach(relativePaths, options, (filePath) =>
                {
                    if (!filePath.StartsWith("D")) {
                        var isolatedPath = filePath.Trim().TrimStart('?', 'M').Trim();
                        var path = Path.Combine(assetRepoRoot, isolatedPath);

                        if (File.Exists(path))
                        {
                            var content = File.ReadAllText(path);
                            var fileDetections = DetectSecrets(content);

                            if (fileDetections != null && fileDetections.Count > 0)
                            {
                                foreach (Detection detection in fileDetections)
                                {
                                    detectedSecrets.Add(Tuple.Create(filePath, detection));
                                }
                            }

                            Interlocked.Increment(ref seen);

                            Console.Write($"\r\u001b[2KScanned {seen}/{total}.");
                        }
                    }
                });

                Console.WriteLine(string.Empty);
            }

            return detectedSecrets.ToList();
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
