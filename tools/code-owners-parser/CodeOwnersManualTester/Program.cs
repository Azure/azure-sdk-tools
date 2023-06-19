using Azure.Sdk.Tools.CodeOwnersParser;

namespace CodeOwnersManualTester
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Multiple repository list
            List<string> repositoryList = new List<string>
            {
                "azure-sdk",
                "azure-sdk-tools",
                "azure-sdk-for-android",
                "azure-sdk-for-c",
                "azure-sdk-for-cpp",
                "azure-sdk-for-go",
                "azure-sdk-for-java",
                "azure-sdk-for-js",
                "azure-sdk-for-net",
                "azure-sdk-for-python"
            };
            // Single Repository List
            //List<string> repositoryList = new List<string>
            //{
            //    "azure-sdk-for-java"
            //};
            foreach (string repository in repositoryList)
            {
                string codeownersUrl = $"https://raw.githubusercontent.com/Azure/{repository}/main/.github/CODEOWNERS";
                List<CodeownersEntry> coEntries = CodeOwnerUtils.GetCodeOwnerEntries(codeownersUrl);
                Console.WriteLine($"Total number of Codeowner entries: {coEntries.Count}");
            }
            // Something to break on
            Console.WriteLine("done");
        }
    }
}
