using System.Collections.Generic;
using System.Text.Json;
using Azure.Sdk.Tools.CodeOwnersParser;
using NUnit.Framework;

namespace Azure.Sdk.Tools.RetrieveCodeOwners.Tests
{
    /// <summary>
    /// This is test of console app Azure.Sdk.Tools.RetrieveCodeOwners.
    /// </summary>
    [TestFixture]
    public class MainTests
    {
        private const string codeOwnerFilePath = "CODEOWNERS";

        private static readonly object[] _sourceLists =
        {
            new object[] {"sdk", false, new List<string> { "person1", "person2" } },
            new object[] { "/sdk", false, new List<string> { "person1", "person2" } },
            new object[] { "sdk/noPath", false, new List<string> { "person1", "person2" } },
            new object[] { "/sdk/azconfig", false, new List<string> { "person3", "person4" } },
            new object[] { "/sdk/azconfig/package", false, new List<string> { "person3", "person4" } },
            new object[] { "/sdk/testUser/", true, new List<string> { "azure-sdk" } },
            new object[] { "/sd", true, new List<string>() }
        };

        [TestCaseSource("_sourceLists")]
        public void TestOnNormalOuput(string targetDirectory, bool includeUserAliasesOnly, List<string> expectedReturn)
        {
            
            using (var consoleOutput = new ConsoleOutput())
            {
                Program.Main(codeOwnerFilePath, targetDirectory, includeUserAliasesOnly);
                var output = consoleOutput.GetOuput();
                TestExpectResult(expectedReturn, output);
                consoleOutput.Dispose();
            }
        }

        [TestCase("PathNotExist")]
        [TestCase("http://testLink")]
        [TestCase("https://testLink")]
        public void TestOnError(string codeOwnerPath)
        {
            Assert.AreEqual(1, Program.Main(codeOwnerPath, "sdk"));
        }

        private void TestExpectResult(List<string> expectReturn, string output)
        {
            CodeOwnerEntry codeOwnerEntry = JsonSerializer.Deserialize<CodeOwnerEntry>(output);
            List<string> actualReturn = codeOwnerEntry.Owners;
            Assert.AreEqual(expectReturn.Count, actualReturn.Count);
            for (int i = 0; i < actualReturn.Count; i++)
            {
                Assert.AreEqual(expectReturn[i], actualReturn[i]);
            }
        }
    }
}
