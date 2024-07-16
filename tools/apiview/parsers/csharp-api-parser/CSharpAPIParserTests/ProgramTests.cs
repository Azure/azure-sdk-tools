using CSharpAPIParser;

namespace CSharpAPIParserTests
{
    public class ProgramTests
    {
        [Theory]
        [InlineData("[2.3.24, 3.0.0)")]
        [InlineData("2.3.24")]
        [InlineData("1.2.0-alpha.20240716.2")]
        [InlineData("1.0.2-preview.17")]
        public void SelectSpecificVersion_Picks_Single_Version(string version)
        {
            var result = Program.SelectSpecificVersion(version);
            var expectedResult = version.Split(',')[0].Trim('[');

            Assert.Equal(result, expectedResult);
        }
    }
}
