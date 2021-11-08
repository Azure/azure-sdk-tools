using System;
using System.IO;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace Azure.Sdk.Tools.RetrieveCodeOwners
{
    class Program
    {
        /// <summary>
        /// Retrieves codeowners information for specific section of the repo
        /// </summary>
        /// <param name="targetDirectory">The directory whose information is to be retrieved</param>
        /// <param name="rootDirectory">The root of the repo or $(Build.SourcesDirectory) on DevOps</param>
        /// <param name="vsoOwningUsers">Variable for setting user aliases</param>
        /// <returns></returns>

        public static void Main(
            string targetDirectory,
            string rootDirectory,
            string vsoOwningUsers
            )
        {
            var target = targetDirectory.ToLower().Trim();
            var codeOwnersLocation = Path.Join(rootDirectory, ".github", "CODEOWNERS");
            var owners = CodeOwnersFile.ParseAndFindOwnersForClosestMatch(codeOwnersLocation, target);
            if (owners == null)
            {
                Console.WriteLine(String.Format("We cannot find any closest code owners from the target path {0}", targetDirectory));
            }
            else
            {
                Console.WriteLine(String.Format("##vso[task.setvariable variable={0};]{1}", vsoOwningUsers, String.Join(",", owners)));
            }
        }
    }
}
