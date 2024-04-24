using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Security.Utilities;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class SecretScanner
    {
        public SecretMasker SecretMasker = new SecretMasker(
            WellKnownRegexPatterns.HighConfidenceMicrosoftSecurityModels.Concat(WellKnownRegexPatterns.LowConfidencePotentialSecurityKeys),
            generateCorrelatingIds: true);

        public SecretScanner() {
            
        }

        public async Task<List<Detection>> DiscoverSecrets(string inputDirectory)
        {
            var files = Directory.GetFiles(inputDirectory, "*", SearchOption.AllDirectories);
            List<Detection> detectedSecrets = new List<Detection>();

            var total = 0;
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
                total++;
            }

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
