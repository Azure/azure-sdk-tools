namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using System.Threading.Tasks;

    public interface IFailureClassifier
    {
        Task ClassifyAsync(FailureAnalyzerContext context);
    }
}
