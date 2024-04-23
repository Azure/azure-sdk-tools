using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Security.Utilities;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public static class SecretScanner
    {
        public static SecretMasker SecretMasker = new SecretMasker(WellKnownRegexPatterns.HighConfidenceMicrosoftSecurityModels, generateCorrelatingIds: true);

        public static async Task<List<Detection>> DiscoverSecrets(string inputDirectory)
        {
            var files = Directory.GetFiles("", "*", SearchOption.AllDirectories);
            List<Detection> detectedSecrets = new List<Detection>();

            foreach (string filePath in files)
            {
                var content = await ReadFile(filePath);
                var fileDetections = DetectSecrets(content);

                if (fileDetections != null && fileDetections.Count > 0)
                {
                    foreach(Detection detection in fileDetections)
                    {
                        detectedSecrets.Add(detection);
                    }
                }
            }

            return detectedSecrets;
        }

        private static async Task<string> ReadFile(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private static ICollection<Detection> DetectSecrets(string stringContent)
        {
            return SecretMasker.DetectSecrets(stringContent);
        }
        
    }
}
