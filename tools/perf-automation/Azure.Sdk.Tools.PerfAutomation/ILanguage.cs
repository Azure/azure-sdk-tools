using Azure.Sdk.Tools.PerfAutomation.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public interface ILanguage
    {
        Task<(string output, string error, object context)> SetupAsync(string project, string languageVersion, IDictionary<string, string> packageVersions);
        Task<IterationResult> RunAsync(string project, string languageVersion, IDictionary<string, string> packageVersions, string testName, string arguments, object context);
        Task CleanupAsync(string project);
        IDictionary<string, string> FilterRuntimePackageVersions(IDictionary<string, string> runtimePackageVersions);
    }
}
