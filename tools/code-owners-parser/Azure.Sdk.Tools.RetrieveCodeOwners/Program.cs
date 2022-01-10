using System;
using System.Text.Json;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace Azure.Sdk.Tools.RetrieveCodeOwners
{
    /// <summary>
    /// The tool command to retrieve code owners.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Retrieves codeowners information for specific section of the repo
        /// </summary>
        /// <param name="codeOwnerFilePath">The path of CODEOWNERS file in repo</param>
        /// <param name="targetDirectory">The directory whose information is to be retrieved</param>
        /// <param name="filterOutNonUserAliases">The option to filter out code owner team alias.</param>
        /// <returns>Exit code</returns>

        public static int Main(
            string codeOwnerFilePath,
            string targetDirectory,
            bool filterOutNonUserAliases = false
            )
        {
            var target = targetDirectory.ToLower().Trim();
            try {
                var codeOwnerEntry = CodeOwnersFile.ParseAndFindOwnersForClosestMatch(codeOwnerFilePath, target);
                if (filterOutNonUserAliases)
                {
                    codeOwnerEntry.FilterOutNonUserAliases();
                }
                var codeOwnerJson = JsonSerializer.Serialize<CodeOwnerEntry>(codeOwnerEntry, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(codeOwnerJson);
                return 0;
            }
            catch (Exception e) {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
        }
    }
}
