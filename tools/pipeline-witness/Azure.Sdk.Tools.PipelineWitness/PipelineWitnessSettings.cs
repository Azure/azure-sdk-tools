namespace Azure.Sdk.Tools.PipelineWitness
{
    public class PipelineWitnessSettings
    {
        public int BuildLogBundleSize { get; set; } = 50;
        public string BuildLogBundlesQueueName { get; set; }
    }
}
