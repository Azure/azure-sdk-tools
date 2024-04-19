using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    public interface IFailureClassifier
    {
        Task ClassifyAsync(FailureAnalyzerContext context);
    }
}
