using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace CodeOwnersManualTester
{
    internal class CodeOwnerUtils
    {
        /// <summary>
        /// Wrapper function to load the CODEOWNERS file from a given path or URL and return
        /// the list of codeowners entries.
        /// </summary>
        /// <param name="codeOwnersFilePath"></param>
        /// <returns>List of CodeownersEntry</returns>
        public static List<CodeownersEntry> GetCodeOwnerEntries(string codeOwnersFilePath)
        {
            Console.WriteLine($"Loading codeowners file, {codeOwnersFilePath}");
            return CodeownersFile.GetCodeownersEntriesFromFileOrUrl(codeOwnersFilePath);
        }
    }
}
