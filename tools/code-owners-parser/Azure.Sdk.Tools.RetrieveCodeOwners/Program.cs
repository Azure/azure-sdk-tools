using System;
using System.Text.Json;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace Azure.Sdk.Tools.RetrieveCodeOwners
{
    class Program
    {
        /// <summary>
        /// Retrieves codeowners information for specific section of the repo
        /// </summary>
        /// <param name="codeOwnerFilePath">The path of CODEOWNERS file in repo</param>
        /// <param name="targetDirectory">The directory whose information is to be retrieved</param>
        /// <returns>Exit code</returns>

        public static int Main(
            string codeOwnerFilePath,
            string targetDirectory
            )
        {
            var target = targetDirectory.ToLower().Trim();
            try {
                var codeOwnerEntry = CodeOwnersFile.ParseAndFindOwnersForClosestMatch(codeOwnerFilePath, target);
                if (codeOwnerEntry == null)
                {
                    Console.Error.WriteLine(String.Format("We cannot find any matching code owners from the target path {0}", targetDirectory));
                    return 1;
                }
                else
                {
                    var codeOwnerJson = JsonSerializer.Serialize<CodeOwnerEntry>(codeOwnerEntry, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(codeOwnerJson);
                    return 0;
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
        }
    }
}
