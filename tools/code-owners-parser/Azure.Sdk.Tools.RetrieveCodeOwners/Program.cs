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
        /// Given targetPath and CODEOWNERS file path or https url codeownersFilePathOrUrl,
        /// prints out to stdout owners of the targetPath as determined by the CODEOWNERS data.
        /// </summary>
        /// <param name="targetPath">The path whose owners are to be determined.</param>
        /// <param name="codeownersFilePathOrUrl">The https url or path to the CODEOWNERS file.</param>
        /// <param name="excludeNonUserAliases">Whether owners that aren't users should be excluded from the
        /// returned owners.</param>
        /// <returns>
        /// On STDOUT: The JSON representation of the matched CodeownersEntry.
        /// "new CodeownersEntry()" if no path in the CODEOWNERS data matches.
        /// <br/><br/>
        /// From the Main method: exit code. 0 if successful, 1 if error.
        /// </returns>
        public static int Main(
            string targetPath,
            string codeownersFilePathOrUrl,
            bool excludeNonUserAliases = false)
        {
            targetPath = targetPath.Trim();
            try 
            {
                var codeownersEntry = CodeownersFile.GetMatchingCodeownersEntry(targetPath, codeownersFilePathOrUrl);
                if (excludeNonUserAliases)
                {
                    codeownersEntry.ExcludeNonUserAliases();
                }

                var codeownersJson = JsonSerializer.Serialize<CodeownersEntry>(
                    codeownersEntry,
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
