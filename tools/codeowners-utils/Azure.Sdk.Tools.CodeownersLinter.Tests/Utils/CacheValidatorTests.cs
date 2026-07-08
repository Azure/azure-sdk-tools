using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Utils
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class CacheValidatorTests
    {
        [Test]
        public void ThrowIfCacheOlderThan_FreshRemoteCache_DoesNotThrow()
        {
            using HttpClient httpClient = CreateHttpClient(DateTimeOffset.UtcNow.AddHours(-1));
            ICacheValidator cacheValidator = new CacheValidator(httpClient);

            Assert.DoesNotThrowAsync(async () =>
                await cacheValidator.ThrowIfCacheOlderThan(DefaultStorageConstants.TeamUserBlobUri, DateTime.UtcNow.AddHours(-6), CancellationToken.None));
        }

        [Test]
        public void ThrowIfCacheOlderThan_StaleRemoteCache_ThrowsHelpfulError()
        {
            using HttpClient httpClient = CreateHttpClient(DateTimeOffset.UtcNow.AddHours(-7));
            ICacheValidator cacheValidator = new CacheValidator(httpClient);

            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await cacheValidator.ThrowIfCacheOlderThan(DefaultStorageConstants.TeamUserBlobUri, DateTime.UtcNow.AddHours(-6), CancellationToken.None));

            Assert.That(ex.Message, Does.Contain(DefaultStorageConstants.TeamUserBlobUri));
        }

        [Test]
        public void ThrowIfCacheOlderThan_LocalFileSource_UsesFileTimestamp()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "{}");
                File.SetLastWriteTimeUtc(tempFile, DateTime.UtcNow.AddHours(-7));
                ICacheValidator cacheValidator = new CacheValidator(CreateHttpClient(DateTimeOffset.UtcNow));

                InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await cacheValidator.ThrowIfCacheOlderThan(tempFile, DateTime.UtcNow.AddHours(-6), CancellationToken.None));

                Assert.That(ex.Message, Does.Contain(tempFile));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        private static HttpClient CreateHttpClient(DateTimeOffset lastModifiedUtc)
        {
            return new HttpClient(new TestHttpMessageHandler(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
                response.Content.Headers.LastModified = lastModifiedUtc;
                return response;
            }));
        }

        private sealed class TestHttpMessageHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Head));
                return Task.FromResult(responseFactory());
            }
        }
    }
}
