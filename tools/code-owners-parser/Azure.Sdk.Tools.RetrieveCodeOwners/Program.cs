using System;
using System.Text.Json;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace Azure.Sdk.Tools.RetrieveCodeOwners
{
    /// <summary>
    /// See Program.Main comment.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Given targetPath and CODEOWNERS file codeownersFilePath,
        /// prints out to stdout owners of the targetPath as determined by the CODEOWNERS file.
        /// </summary>
        /// <param name="targetPath">The path whose owners are to be determined.</param>
        /// <param name="codeownersFilePath">The path to the CODEOWNERS file with ownership information.</param>
        /// <param name="excludeNonUserAliases">Whether owners that aren't users should be excluded from the returned owners.</param>
        /// <returns>Exit code</returns>
        public static int Main(
            string targetPath,
            string codeownersFilePath,
            bool excludeNonUserAliases = false)
        {
            targetPath = targetPath.Trim();
            try 
            {
                var codeownersEntry = CodeownersFile.GetMatchingCodeownersEntry(targetPath, codeownersFilePath);
                if (excludeNonUserAliases)
                {
                    codeownersEntry.ExcludeNonUserAliases();
                }

                string codeownersJson = JsonSerializer.Serialize<CodeownersEntry>(codeownersEntry,
                    new JsonSerializerOptions { WriteIndented = true });

                Console.WriteLine(codeownersJson);
                return 0;
            }
            catch (Exception e) 
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
        }
    }
}
