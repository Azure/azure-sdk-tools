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

        [Theory]
        [InlineData("C#", "dotnet")]
        [InlineData("cs", "dotnet")]
        [InlineData("csharp", "dotnet")]
        [InlineData("dotnet", "dotnet")]
        [InlineData(".net", "dotnet")]
        [InlineData("net", "dotnet")]
        [InlineData("python", "python")]
        [InlineData("py", "python")]
        [InlineData("Python", "python")]
        [InlineData("javascript", "typescript")]
        [InlineData("js", "typescript")]
        [InlineData("typescript", "typescript")]
        [InlineData("ts", "typescript")]
        [InlineData("go", "golang")]
        [InlineData("golang", "golang")]
        [InlineData("Go", "golang")]
        [InlineData("java", "java")]
        [InlineData("Java", "java")]
        [InlineData("swift", "ios")]
        [InlineData("Swift", "ios")]
        [InlineData("c", "clang")]
        [InlineData("c++", "cpp")]
        [InlineData("cpp", "cpp")]
        [InlineData("rust", "rust")]
        [InlineData("Rust", "rust")]
        public void GetLanguageAliasForCopilotService_MapsCorrectly(string input, string expected)
        {
            var result = LanguageServiceHelpers.GetLanguageAliasForCopilotService(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetLanguageAliasForCopilotService_NullOrEmptyInput_ReturnsNull(string input)
        {
            var result = LanguageServiceHelpers.GetLanguageAliasForCopilotService(input);
            Assert.Null(result);
        }

        [Fact]
        public void GetLanguageAliasForCopilotService_UnknownLanguage_ReturnsLowercased()
        {
            var result = LanguageServiceHelpers.GetLanguageAliasForCopilotService("Kotlin");
            Assert.Equal("kotlin", result);
        }

        [Fact]
        public void GetLanguageAliasForCopilotService_JavaWithAndroidVariant_ReturnsAndroid()
        {
            var result = LanguageServiceHelpers.GetLanguageAliasForCopilotService("java", "Android");
            Assert.Equal("android", result);
        }

        [Fact]
        public void GetLanguageAliasForCopilotService_JavaWithoutVariant_ReturnsJava()
        {
            var result = LanguageServiceHelpers.GetLanguageAliasForCopilotService("java");
            Assert.Equal("java", result);
        }
    }
}
