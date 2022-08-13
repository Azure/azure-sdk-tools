using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Azure.Sdk.Tools.PipelineWitness.ApplicationInsights;

using Microsoft.ApplicationInsights;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class DevOpsClient
    {
        private const string OperationType = "Azure DevOps";
        private readonly TelemetryClient telemetryClient;
        private readonly BuildHttpClient buildHttpClient;
        private readonly TestResultsHttpClient testResultsHttpClient;

        public DevOpsClient(TelemetryClient telemetryClient, BuildHttpClient buildHttpClient, TestResultsHttpClient testResultsHttpClient)
        {
            this.telemetryClient = telemetryClient;
            this.buildHttpClient = buildHttpClient ?? throw new ArgumentNullException(nameof(buildHttpClient));
            this.testResultsHttpClient = testResultsHttpClient ?? throw new ArgumentNullException(nameof(testResultsHttpClient)); ;
        }

        public virtual async Task<IReadOnlyList<string>> GetBuildLogLinesAsync(Guid project, int buildId, int logId)
        {
            return await this.telemetryClient.TraceAsync(
                nameof(GetBuildLogLinesAsync),
                OperationType,
                () => this.buildHttpClient.GetBuildLogLinesAsync(project, buildId, logId));
        }

        public virtual async Task<Timeline> GetBuildTimelineAsync(Guid project, int buildId)
        {
            return await this.telemetryClient.TraceAsync(
                nameof(GetBuildTimelineAsync),
                OperationType,
                () => this.buildHttpClient.GetBuildTimelineAsync(project, buildId));
        }

        public virtual async Task<List<BuildLog>> GetBuildLogsAsync(Guid project, int buildId)
        {
            return await this.telemetryClient.TraceAsync(
                nameof(GetBuildLogsAsync),
                OperationType,
                () => this.buildHttpClient.GetBuildLogsAsync(project, buildId));
        }

        public virtual async Task<Stream> GetArtifactContentZipAsync(Guid project, int buildId, string artifactName)
        {
            return await this.telemetryClient.TraceAsync(
                nameof(GetArtifactContentZipAsync),
                OperationType,
                () => this.buildHttpClient.GetArtifactContentZipAsync(project,
                    buildId,
                    artifactName));
        }

        public virtual async Task<IPagedList<BuildDefinition>> GetFullDefinitionsAsync2(string project)
        {
            return await this.telemetryClient.TraceAsync(
                nameof(GetFullDefinitionsAsync2),
                OperationType,
                () => this.buildHttpClient.GetFullDefinitionsAsync2(project: project));
        }

        public virtual async Task<IPagedList<TestRun>> QueryTestRunsAsync2(Guid projectId, DateTime rangeStart, DateTime rangeEnd, string continuationToken, int[] buildIds)
        {
            return await this.telemetryClient.TraceAsync(
                nameof(QueryTestRunsAsync2),
                OperationType,
                () => this.testResultsHttpClient.QueryTestRunsAsync2(projectId, rangeStart, rangeEnd, continuationToken: continuationToken, buildIds: buildIds));
        }

        public virtual async Task<Build> GetBuildAsync(Guid project, int buildId)
        {
            return await this.telemetryClient.TraceAsync(
                nameof(GetBuildAsync),
                OperationType,
                () => this.buildHttpClient.GetBuildAsync(project, buildId));
        }

        public virtual async Task<Stream> GetBuildLogAsync(Guid project, int buildId, int logId)
        {
            return await this.telemetryClient.TraceAsync(
                nameof(GetBuildLogAsync),
                OperationType,
                () => this.buildHttpClient.GetBuildLogAsync(project, buildId, logId));
        }
    }
}
