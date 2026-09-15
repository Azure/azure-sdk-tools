using APIViewWeb.Repositories;
using Xunit;

namespace APIViewUnitTests
{
    public class DevopsArtifactUrlTests
    {
        private const string DownloadUrl = "https://artprodcus3.artifacts.visualstudio.com/_apis/artifact/abc/content?format=zip";

        [Fact]
        public void BuildArtifactDownloadUrlKeepsAnOrdinaryPathIntact()
        {
            var url = DevopsArtifactRepository.BuildArtifactDownloadUrl(DownloadUrl, "file", "/artifacts/package.json");

            Assert.Equal(
                "https://artprodcus3.artifacts.visualstudio.com/_apis/artifact/abc/content?format=file&subPath=%2Fartifacts%2Fpackage.json",
                url);
        }

        [Fact]
        public void BuildArtifactDownloadUrlAddsTheLeadingSeparator()
        {
            var url = DevopsArtifactRepository.BuildArtifactDownloadUrl(DownloadUrl, "file", "artifacts/package.json");

            Assert.EndsWith("&subPath=%2Fartifacts%2Fpackage.json", url);
        }

        [Fact]
        public void BuildArtifactDownloadUrlDropsAnyExistingQuery()
        {
            var url = DevopsArtifactRepository.BuildArtifactDownloadUrl(DownloadUrl, "zip", "/a.json");

            Assert.StartsWith("https://artprodcus3.artifacts.visualstudio.com/_apis/artifact/abc/content?format=zip&subPath=", url);
            Assert.Single(url.Split('?'), part => part.Contains("format="));
        }

        // A file path reaches this from the request. Without escaping, these values
        // would add or truncate parameters on a request that carries the service's
        // Azure DevOps credentials.
        [Theory]
        [InlineData("/a&format=zip/package.json")]
        [InlineData("/a&subPath=/etc/package.json")]
        [InlineData("/a#fragment/package.json")]
        [InlineData("/a?other=1/package.json")]
        [InlineData("/a=b/package.json")]
        [InlineData("/a b/package.json")]
        public void BuildArtifactDownloadUrlDoesNotLetAPathAddOrTruncateParameters(string filePath)
        {
            var url = DevopsArtifactRepository.BuildArtifactDownloadUrl(DownloadUrl, "file", filePath);

            var query = url.Substring(url.IndexOf('?') + 1);
            var parameters = query.Split('&');

            Assert.Equal(2, parameters.Length);
            Assert.Equal("format=file", parameters[0]);
            Assert.StartsWith("subPath=", parameters[1]);
            Assert.DoesNotContain("#", url);
        }

        [Fact]
        public void BuildArtifactDownloadUrlRoundTripsThePathThroughTheQuery()
        {
            const string filePath = "/a&b/package.json";

            var url = DevopsArtifactRepository.BuildArtifactDownloadUrl(DownloadUrl, "file", filePath);

            var subPath = url.Substring(url.IndexOf("&subPath=") + "&subPath=".Length);
            Assert.Equal(filePath, System.Uri.UnescapeDataString(subPath));
        }
    }
}
