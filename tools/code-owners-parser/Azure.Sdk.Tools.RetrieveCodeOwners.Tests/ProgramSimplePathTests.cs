using System.Collections.Generic;
using System.Text.Json;
using Azure.Sdk.Tools.CodeOwnersParser;
using NUnit.Framework;

namespace Azure.Sdk.Tools.RetrieveCodeOwners.Tests
{
    /// <summary>
    /// Test class for Azure.Sdk.Tools.RetrieveCodeOwners.Program.Main(),
    /// for scenario in which targetPath is a simple path, i.e.
    /// targetPath.IsGlobPath() returns false.
    /// </summary>
    [TestFixture]
    public class ProgramSimplePathTests
    {
        private const string CodeownersFilePath = "./TestData/simple_path_CODEOWNERS";

        private static readonly object[] sourceLists =
        {
            new object[] {"sdk", false, new List<string> { "person1", "person2" } },
            new object[] { "/sdk", false, new List<string> { "person1", "person2" } },
            new object[] { "sdk/noPath", false, new List<string> { "person1", "person2" } },
            new object[] { "/sdk/azconfig", false, new List<string> { "person3", "person4" } },
            new object[] { "/sdk/azconfig/package", false, new List<string> { "person3", "person4" } },
            new object[] { "/sdk/testUser/", true, new List<string> { "azure-sdk" } },
            new object[] { "/sd", true, new List<string>() }
        };

        [TestCaseSource(nameof(sourceLists))]
        public void OutputsCodeownersForSimplePath(
            string targetPath,
            bool excludeNonUserAliases,
            List<string> expectedOwners)
        {
            using var consoleOutput = new ConsoleOutput();

            // Act
            Program.Main(targetPath, CodeownersFilePath, excludeNonUserAliases);

            string actualOutput = consoleOutput.GetStdout();
            AssertOwners(actualOutput, expectedOwners);
        }

        [TestCase("PathNotExist")]
        [TestCase("http://testLink")]
        [TestCase("https://testLink")]
        public void ErrorsOutOnInvalidInputs(string codeownersPath)
        {
            Assert.That(Program.Main("sdk", codeownersPath), Is.EqualTo(1));
        }

        private static void AssertOwners(string actualOutput, List<string> expectedOwners)
        {
            CodeownersEntry? actualEntry = JsonSerializer.Deserialize<CodeownersEntry>(actualOutput);
            List<string> actualOwners = actualEntry!.Owners;
            Assert.That(actualOwners, Has.Count.EqualTo(expectedOwners.Count));
            for (int i = 0; i < actualOwners.Count; i++)
            {
                Assert.That(actualOwners[i], Is.EqualTo(expectedOwners[i]));
            }
        }
    }
}
