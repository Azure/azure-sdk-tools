using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class EnhancedBuildHttpClient : BuildHttpClient

    {
        public EnhancedBuildHttpClient(Uri baseUrl, VssCredentials credentials)
            : base(baseUrl, credentials)
        {}

        public EnhancedBuildHttpClient(Uri baseUrl, VssCredentials credentials, VssHttpRequestSettings settings)
            : base(baseUrl, credentials, settings)
        {}

        public EnhancedBuildHttpClient(Uri baseUrl, VssCredentials credentials, params DelegatingHandler[] handlers)
            : base(baseUrl, credentials, handlers)
        {}

        public EnhancedBuildHttpClient(Uri baseUrl, VssCredentials credentials, VssHttpRequestSettings settings, params DelegatingHandler[] handlers)
            : base(baseUrl, credentials, settings, handlers)
        {}

        public EnhancedBuildHttpClient(Uri baseUrl, HttpMessageHandler pipeline, bool disposeHandler)
            : base(baseUrl, pipeline, disposeHandler)
        {}

        public override async Task<Stream> GetArtifactContentZipAsync(
            Guid project,
            int buildId,
            string artifactName,
            object userState = null,
            CancellationToken cancellationToken = default)
        {
            var artifact = await base.GetArtifactAsync(project, buildId, artifactName, userState, cancellationToken);
            return await GetArtifactContentZipAsync(artifact, cancellationToken);
        }

        public override async Task<Stream> GetArtifactContentZipAsync(
            string project,
            int buildId,
            string artifactName,
            object userState = null,
            CancellationToken cancellationToken = default)
        {
            var artifact = await base.GetArtifactAsync(project, buildId, artifactName, userState, cancellationToken);
            return await GetArtifactContentZipAsync(artifact, cancellationToken);
        }

        private async Task<Stream> GetArtifactContentZipAsync(BuildArtifact artifact, CancellationToken cancellationToken)
        {
            var downloadUrl = artifact?.Resource?.DownloadUrl;
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidArtifactDataException("Artifact contained no download url");
            }

            var responseStream = await Client.GetStreamAsync(downloadUrl, cancellationToken);
            return responseStream;
        }
    }
}
