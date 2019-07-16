using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Sdk.Tools.VersionGuard
{
    /// <summary>Program entry point for Version Guard.</summary>
    public class Program
    {
        /// <summary>Version Guard</summary>
        /// <param name="packageName">Name of the package to scan for.</param>
        /// <param name="packageVersion">Version of the package being proposed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public static async Task<int> Main(string packageName, string packageVersion, CancellationToken cancellationToken)
        {
            return await Task.FromResult(0);
        }
    }
}
