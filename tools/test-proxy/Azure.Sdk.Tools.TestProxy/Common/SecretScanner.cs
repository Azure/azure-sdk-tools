using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Security.Utilities;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public static class SecretScanner
    {
        public static SecretMasker SecretMasker = new SecretMasker(WellKnownRegexPatterns.HighConfidenceMicrosoftSecurityModels, generateCorrelatingIds: true);

        public static async Task<List<string>> DiscoverSecrets(string inputDirectory)
        {
            var files = Directory.GetFiles("", "*", SearchOption.AllDirectories);
            List<string> detectedSecrets = new List<string>();

            foreach (string filePath in files)
            {
                var content = await ReadFile(filePath);
                var fileDetections = await DetectSecrets(content);

                if (!string.IsNullOrWhiteSpace(fileDetections))
                {
                    detectedSecrets.Add(fileDetections);
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

        private static async Task<string> DetectSecrets(string stringContent)
        {
            await Task.Delay(100);
            return "hah!";
        }
        
    }
}
