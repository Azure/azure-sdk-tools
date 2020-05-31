using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines
{
    public class Run
    {
        [JsonPropertyName("id")]
        public string Id => $"{ProjectId}/{PipelineId}/{RunId}";

        [JsonPropertyName("runId")]
        public int RunId { get; set; }

        [JsonPropertyName("runName")]
        public string RunName { get; set; }

        [JsonPropertyName("runUrl")]
        public Uri RunUrl { get; set; }

        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("projectName")]
        public string ProjectName { get; set; }

        [JsonPropertyName("projectUrl")]
        public Uri ProjectUrl { get; set; }

        [JsonPropertyName("pipelineId")]
        public int PipelineId { get; set; }

        [JsonPropertyName("pipelineName")]
        public string PipelineName { get; set; }

        [JsonPropertyName("pipelineUrl")]
        public Uri PipelineUrl { get; set; }

        [JsonPropertyName("repositoryId")]
        public string RepositoryId { get; set; }

        [JsonPropertyName("reason")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RunReason Reason { get; set; }

        [JsonPropertyName("result")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RunResult Result { get; set; }

        [JsonPropertyName("state")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RunStatus State { get; set; }

        [JsonPropertyName("gitRef")]
        public string GitReference { get; set; }

        [JsonPropertyName("sha")]
        public string GitCommitSha { get; set; }

        [JsonPropertyName("startTime")]
        public DateTimeOffset StartTime { get; set; }

        [JsonPropertyName("finishTime")]
        public DateTimeOffset FinishTime { get; set; }

        [JsonPropertyName("agentDurationInSeconds")]
        public double AgentDurationInSeconds { get; set; }

        [JsonPropertyName("queueDurationInSeconds")]
        public double QueueDurationInSeconds { get; set; }
    }
}
