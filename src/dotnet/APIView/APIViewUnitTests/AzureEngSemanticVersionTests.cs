using System.Collections.Generic;
using Xunit;
using APIView.Model.V2;

namespace APIViewUnitTests
{
    public class AzureEngSemanticVersionTests
    {
        [Fact]
        public void SortVersionStrings_ShouldSortCorrectly()
        {
            var versions = new List<string>
        {
            "1.0.1",
            "2.0.0",
            "2.0.0-alpha.20200920",
            "2.0.0-alpha.20200920.1",
            "2.0.0-beta.2",
            "1.0.10",
            "2.0.0-alpha.20201221.03",
            "2.0.0-alpha.20201221.1",
            "2.0.0-alpha.20201221.5",
            "2.0.0-alpha.20201221.2",
            "2.0.0-alpha.20201221.10",
            "2.0.0-beta.1",
            "2.0.0-beta.10",
            "1.0.0",
            "1.0.0b2",
            "1.0.2"
        };

            var expectedSort = new List<string>
        {
            "2.0.0",
            "2.0.0-beta.10",
            "2.0.0-beta.2",
            "2.0.0-beta.1",
            "2.0.0-alpha.20201221.10",
            "2.0.0-alpha.20201221.5",
            "2.0.0-alpha.20201221.03",
            "2.0.0-alpha.20201221.2",
            "2.0.0-alpha.20201221.1",
            "2.0.0-alpha.20200920.1",
            "2.0.0-alpha.20200920",
            "1.0.10",
            "1.0.2",
            "1.0.1",
            "1.0.0",
            "1.0.0b2"
        };

            var sortedVersions = AzureEngSemanticVersion.SortVersionStrings(versions);
            Assert.Equal(expectedSort, sortedVersions);
        }

        [Fact]
        public void ParseAlphaVersion_ShouldParseCorrectly()
        {
            var alphaVerString = "1.2.3-alpha.20200828.9";
            var alphaVer = new AzureEngSemanticVersion(alphaVerString);
            Assert.True(alphaVer.IsPrerelease);
            Assert.Equal(1, alphaVer.Major);
            Assert.Equal(2, alphaVer.Minor);
            Assert.Equal(3, alphaVer.Patch);
            Assert.Equal("alpha", alphaVer.PrereleaseLabel);
            Assert.Equal(20200828, alphaVer.PrereleaseNumber);
            Assert.Equal("9", alphaVer.BuildNumber);
            Assert.Equal(alphaVerString, alphaVer.ToString());
        }

        [Fact]
        public void ParsePythonAlphaVersion_ShouldParseCorrectly()
        {
            var pythonAlphaVerString = "1.2.3a20200828009";
            var pythonAlphaVer = new AzureEngSemanticVersion(pythonAlphaVerString, "python");
            Assert.True(pythonAlphaVer.IsPrerelease);
            Assert.Equal(1, pythonAlphaVer.Major);
            Assert.Equal(2, pythonAlphaVer.Minor);
            Assert.Equal(3, pythonAlphaVer.Patch);
            Assert.Equal("a", pythonAlphaVer.PrereleaseLabel);
            Assert.Equal(20200828, pythonAlphaVer.PrereleaseNumber);
            Assert.Equal("009", pythonAlphaVer.BuildNumber);
            Assert.Equal(pythonAlphaVerString, pythonAlphaVer.ToString());
        }

        [Theory]
        // Test prerelease detection - any version with prelabel is prerelease
        [InlineData("1.0.0-beta.1", "csharp", true)]
        [InlineData("1.0.0-alpha.1", "csharp", true)]
        [InlineData("1.0.0-rc.1", "csharp", true)]
        [InlineData("1.0.0-preview.1", "csharp", true)]
        // Test case insensitivity 
        [InlineData("1.0.0-BETA.1", "csharp", true)]
        // Test Python specific format (no separators)
        [InlineData("1.0.0b1", "python", true)]
        [InlineData("1.0.0a1", "python", true)]
        [InlineData("1.0.0rc1", "python", true)]
        // Test 0.x versions are considered prerelease even without prelabel
        [InlineData("0.1.0", "csharp", true)]
        [InlineData("0.9.99", "python", true)]
        // Test stable versions
        [InlineData("1.0.0", "csharp", false)]
        [InlineData("2.5.1", "python", false)]
        // Test that language doesn't affect prerelease detection for standard formats
        [InlineData("1.0.0-beta.1", "javascript", true)]
        [InlineData("1.0.0", "unknown", false)]
        public void IsPrerelease_ShouldDetectCorrectly(string version, string language, bool expectedIsPrerelease)
        {
            var semanticVersion = new AzureEngSemanticVersion(version, language);
            Assert.Equal(expectedIsPrerelease, semanticVersion.IsPrerelease);
        }

        [Theory]
        // Test edge cases and malformed versions
        [InlineData("", "csharp", false)]
        [InlineData("1", "csharp", false)]
        [InlineData("not-a-version", "csharp", false)]
        public void IsPrerelease_EdgeCases_ShouldHandleCorrectly(string version, string language, bool expectedIsPrerelease)
        {
            var semanticVersion = new AzureEngSemanticVersion(version, language);
            Assert.Equal(expectedIsPrerelease, semanticVersion.IsPrerelease);
        }

        [Theory]
        [InlineData("1.2.0-alpha.20240305.1", true)]  // standard CI build with sequence suffix
        [InlineData("1.2.0-alpha.20240305",   true)]  // standard CI build without sequence suffix
        [InlineData("1.2.3a20200828009",      true)]  // Python CI build with 3-digit sequence
        [InlineData("1.1.0a20260101",         true)]  // Python CI build without sequence suffix
        [InlineData("1.2.0-beta.1",           false)] // intentional beta prerelease
        [InlineData("1.2.0-rc.2",             false)] // intentional RC prerelease
        [InlineData("1.0.0b1",               false)] // Python intentional beta
        [InlineData("1.0.0a1",               false)] // Python intentional alpha (small number)
        [InlineData("1.2.0",                 false)] // stable release
        [InlineData("",                      false)] // empty string
        public void IsDailyDevBuild_ShouldDetectCorrectly(string version, bool expected)
        {
            Assert.Equal(expected, new AzureEngSemanticVersion(version).IsDailyDevBuild);
        }
    }
}
