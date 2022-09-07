using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class JavaScriptTests
    {
        [Test]
        public void GetRuntimePackageVersions()
        {
            var standardOutput = File.ReadAllText("stdout/js.txt");
            var actual = JavaScript.GetRuntimePackageVersions(standardOutput);

            var expected = new Dictionary<string, string>() {
                { "@azure-tests/perf-storage-blob", "1.0.0 /mnt/vss/_work/1/s/sdk/storage/perf-tests/storage-blob" },
                { "@azure/core-http", "2.2.8 -> /mnt/vss/_work/1/s/sdk/core/core-http" },
                { "@azure/abort-controller", "1.1.1" },
                { "@azure/dev-tool", "1.0.0" },
                { "@azure/eslint-plugin-azure-sdk", "3.0.0" },
                { "@azure/core-auth", "1.4.1" },
                { "@azure/core-util", "1.1.1" },
                { "@azure/core-tracing", "1.0.0-preview.13, 1.0.2" },
                { "@azure/logger", "1.0.4" },
                { "@azure/logger-js", "1.3.2" },
                { "@azure/core-rest-pipeline", "1.9.3 -> /mnt/vss/_work/1/s/sdk/core/core-rest-pipeline" },
                { "@azure/storage-blob", "12.11.0" },
                { "@azure/test-utils-perf", "1.0.0 -> /mnt/vss/_work/1/s/sdk/test-utils/perf" },
                { "@azure/core-client", "1.6.2" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }

        [Test]
        public void FilterRuntimePackageVersions()
        {
            var allPackages = new Dictionary<string, string>() {
                { "@azure-tests/perf-storage-blob", "1.0.0 /mnt/vss/_work/1/s/sdk/storage/perf-tests/storage-blob" },
                { "@azure/core-http", "2.2.8 -> /mnt/vss/_work/1/s/sdk/core/core-http" },
                { "@azure/abort-controller", "1.1.1" },
                { "@azure/dev-tool", "1.0.0" },
                { "@azure/eslint-plugin-azure-sdk", "3.0.0" },
                { "@azure/core-auth", "1.4.1" },
                { "@azure/core-util", "1.1.1" },
                { "@azure/core-tracing", "1.0.0-preview.13, 1.0.2" },
                { "@azure/logger", "1.0.4" },
                { "@azure/logger-js", "1.3.2" },
                { "@azure/core-rest-pipeline", "1.9.3 -> /mnt/vss/_work/1/s/sdk/core/core-rest-pipeline" },
                { "@azure/storage-blob", "12.11.0" },
                { "@azure/test-utils-perf", "1.0.0 -> /mnt/vss/_work/1/s/sdk/test-utils/perf" },
                { "@azure/core-client", "1.6.2" },
            };

            var actual = (new JavaScript()).FilterRuntimePackageVersions(allPackages);

            var expected = new Dictionary<string, string>() {
                { "@azure/core-http", "2.2.8 -> /mnt/vss/_work/1/s/sdk/core/core-http" },
                { "@azure/abort-controller", "1.1.1" },
                { "@azure/dev-tool", "1.0.0" },
                { "@azure/eslint-plugin-azure-sdk", "3.0.0" },
                { "@azure/core-auth", "1.4.1" },
                { "@azure/core-util", "1.1.1" },
                { "@azure/core-tracing", "1.0.0-preview.13, 1.0.2" },
                { "@azure/logger", "1.0.4" },
                { "@azure/logger-js", "1.3.2" },
                { "@azure/core-rest-pipeline", "1.9.3 -> /mnt/vss/_work/1/s/sdk/core/core-rest-pipeline" },
                { "@azure/storage-blob", "12.11.0" },
                { "@azure/core-client", "1.6.2" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }
    }
}
