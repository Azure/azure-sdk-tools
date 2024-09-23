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
    }
}
