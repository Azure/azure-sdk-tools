using APIViewWeb.Helpers;
using Xunit;

namespace APIViewUnitTests
{
    public class LanguageServiceHelpersTests
    {
        [Theory]
        [InlineData("net", "C#")]
        [InlineData(".NET", "C#")]
        [InlineData(".Net", "C#")]
        [InlineData("cpp", "C++")]
        [InlineData("js", "JavaScript")]
        [InlineData("JS", "JavaScript")]
        [InlineData("Js", "JavaScript")]
        [InlineData("Cadl", "TypeSpec")]
        [InlineData("Python", "Python")]
        [InlineData("python", "Python")]
        [InlineData("Go", "Go")]
        [InlineData("go", "Go")]
        [InlineData("java", "Java")]
        public void MapLanguageAlias_MapsCorrectly(string input, string expected)
        {
            var result = LanguageServiceHelpers.MapLanguageAlias(input);
            Assert.Equal(expected, result);
        }
    }
}
