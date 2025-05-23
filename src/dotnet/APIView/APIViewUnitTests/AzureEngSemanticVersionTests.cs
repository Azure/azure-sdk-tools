using System.Collections.Generic;
using Xunit;

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
    }
}
