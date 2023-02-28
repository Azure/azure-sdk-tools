using Azure.Sdk.Tools.PerfAutomation.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public interface ILanguage
    {
        string WorkingDirectory { get; set; }

        Task CleanupAsync(string project);

        IDictionary<string, string> FilterRuntimePackageVersions(IDictionary<string, string> runtimePackageVersions);

        Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            bool debug);

        Task<IterationResult> RunAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            string testName,
            string arguments,
            bool profile,
            string profilerOptions,
            object context);
    }
}
