using Azure.Sdk.Tools.PerfAutomation.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public abstract class LanguageBase : ILanguage
    {
        protected abstract Language Language { get; }

        protected string ProfileDirectory => Path.GetFullPath(Path.Combine(WorkingDirectory, Language + "-profile"));

        protected string WorkingDirectory => Program.Config.WorkingDirectories[Language];

        public abstract Task CleanupAsync(string project);

        public virtual IDictionary<string, string> FilterRuntimePackageVersions(IDictionary<string, string> runtimePackageVersions)
            => runtimePackageVersions;

        public abstract Task<IterationResult> RunAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions,
            string testName,
            string arguments,
            bool profile,
            object context);

        public abstract Task<(string output, string error, object context)> SetupAsync(
            string project,
            string languageVersion,
            string primaryPackage,
            IDictionary<string, string> packageVersions);
    }
}
