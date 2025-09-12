namespace CSharpAPIParserTests
{
    public class ProgramTests
    {
        [Theory]
        [InlineData("1.0.0.0", "1.0.0.0")]
        [InlineData("[1.0.0.0,)", "1.0.0.0")]
        [InlineData("(1.0.0.0,)", "1.0.0.0")]
        [InlineData("[1.0.0.0]", "1.0.0.0")]
        [InlineData("(,1.0.0.0]", "1.0.0.0")]
        [InlineData("(,1.0.0.0)", "1.0.0.0")]
        [InlineData("[1.0.0.0,2.0.0.0]", "2.0.0.0")]
        [InlineData("(1.0.0.0,2.0.0.0)", "2.0.0.0")]
        [InlineData("[1.0.0.0,2.0.0.0)", "2.0.0.0")]
        public void SelectSpecificVersion_Picks_Single_Version(string version, string expectedResult)
        {
            var result = Program.SelectSpecificVersion(version);
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("lib/net8.0/System.Text.Json.dll", "net8.0")]
        [InlineData("lib/netstandard2.0/Newtonsoft.Json.dll", "netstandard2.0")]
        [InlineData("lib/net462/Azure.Core.dll", "net462")]
        [InlineData("ref/net6.0/System.Memory.dll", "net6.0")]
        [InlineData("content/readme.txt", null)]
        [InlineData("analyzers/dotnet/cs/analyzer.dll", null)]
        public void ParseTargetFrameworkFromPath_ReturnsCorrectFramework(string path, string expected)
        {
            var result = Program.ParseTargetFrameworkFromPath(path);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(new[] { "net8.0", "net6.0", "netstandard2.0" }, "netstandard2.0")] // Prefer netstandard2.0 for compatibility
        [InlineData(new[] { "net462", "netstandard2.0", "netstandard2.1" }, "netstandard2.0")]
        [InlineData(new[] { "net462", "net48" }, "net48")]
        [InlineData(new[] { "netstandard1.0", "netstandard1.6" }, "netstandard1.6")]
        public void SelectBestTargetFramework_ChoosesBestOption(string[] frameworks, string expected)
        {
            var result = Program.SelectBestTargetFramework(frameworks);
            Assert.Equal(expected, result);
        }
    }
}
