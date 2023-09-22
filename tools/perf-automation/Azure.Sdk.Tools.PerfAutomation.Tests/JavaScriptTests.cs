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
                { "@azure-tests/perf-storage-blob", "1.0.0 /home/user/js/common/deploy/sdk/storage/perf-tests/storage-blob" },
                { "@azure/core-auth", "1.4.0, 1.4.1 -> ./../../../core/core-auth" },
                { "@azure/core-http", "2.2.7, 2.2.8 -> ./../../../core/core-http" },
                { "@azure/core-lro", "2.3.0" },
                { "@azure/core-paging", "1.3.0" },
                { "@azure/core-rest-pipeline", "1.9.3 -> ./../../../core/core-rest-pipeline" },
                { "@azure/core-tracing", "1.0.0-preview.13, 1.0.2 -> ./../../../core/core-tracing" },
                { "@azure/core-util", "1.1.0, 1.1.1 -> ./../../../core/core-util" },
                { "@azure/abort-controller", "1.1.0, 1.1.1 -> ./../../../core/abort-controller" },
                { "@azure/logger", "1.0.3, 1.0.4 -> ./../../../core/logger" },
                { "@azure/storage-blob", "12.11.0" },
                { "@azure/test-utils-perf", "1.0.0 -> ./../../../test-utils/perf" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }

        [Test]
        public void FilterRuntimePackageVersions()
        {
            var allPackages = new Dictionary<string, string>() {
                { "@azure-tests/perf-storage-blob", "1.0.0 /home/user/js/common/deploy/sdk/storage/perf-tests/storage-blob" },
                { "@azure/core-auth", "1.4.0, 1.4.1 -> ./../../../core/core-auth" },
                { "@azure/core-http", "2.2.7, 2.2.8 -> ./../../../core/core-http" },
                { "@azure/core-lro", "2.3.0" },
                { "@azure/core-paging", "1.3.0" },
                { "@azure/core-rest-pipeline", "1.9.3 -> ./../../../core/core-rest-pipeline" },
                { "@azure/core-tracing", "1.0.0-preview.13, 1.0.2 -> ./../../../core/core-tracing" },
                { "@azure/core-util", "1.1.0, 1.1.1 -> ./../../../core/core-util" },
                { "@azure/abort-controller", "1.1.0, 1.1.1 -> ./../../../core/abort-controller" },
                { "@azure/logger", "1.0.3, 1.0.4 -> ./../../../core/logger" },
                { "@azure/storage-blob", "12.11.0" },
                { "@azure/test-utils-perf", "1.0.0 -> ./../../../test-utils/perf" },
            };

            var actual = (new JavaScript()).FilterRuntimePackageVersions(allPackages);

            var expected = new Dictionary<string, string>() {
                { "@azure/core-auth", "1.4.0, 1.4.1 -> ./../../../core/core-auth" },
                { "@azure/core-http", "2.2.7, 2.2.8 -> ./../../../core/core-http" },
                { "@azure/core-lro", "2.3.0" },
                { "@azure/core-paging", "1.3.0" },
                { "@azure/core-rest-pipeline", "1.9.3 -> ./../../../core/core-rest-pipeline" },
                { "@azure/core-tracing", "1.0.0-preview.13, 1.0.2 -> ./../../../core/core-tracing" },
                { "@azure/core-util", "1.1.0, 1.1.1 -> ./../../../core/core-util" },
                { "@azure/abort-controller", "1.1.0, 1.1.1 -> ./../../../core/abort-controller" },
                { "@azure/logger", "1.0.3, 1.0.4 -> ./../../../core/logger" },
                { "@azure/storage-blob", "12.11.0" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }
    }
}
