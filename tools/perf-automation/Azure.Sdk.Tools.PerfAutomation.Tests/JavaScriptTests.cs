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
                { "@azure/core-http", "1.2.6, 2.2.7, 2.2.8 -> ./../../../core/core-http" },
                { "@azure/abort-controller", "1.1.0, 1.1.1 -> ./../../../core/abort-controller" },
                { "@azure/core-asynciterator-polyfill", "1.0.2" },
                { "@azure/core-auth", "1.4.0, 1.4.1 -> ./../../../core/core-auth" },
                { "@azure/core-client", "1.6.1, 1.6.2 -> ./../../../core/core-client" },
                { "@azure/core-lro", "1.0.5, 2.3.0" },
                { "@azure/core-paging", "1.3.0" },
                { "@azure/core-rest-pipeline", "1.9.2, 1.9.3 -> ./../../../core/core-rest-pipeline" },
                { "@azure/core-tracing", "1.0.0-preview.11, 1.0.0-preview.13, 1.0.1, 1.0.2 -> ./../../../core/core-tracing" },
                { "@azure/core-util", "1.1.0, 1.1.1 -> ./../../../core/core-util" },
                { "@azure/core-xml", "1.3.0, 1.3.1 -> ./../../../core/core-xml" },
                { "@azure/dev-tool", "1.0.0 -> ./../../../../common/tools/dev-tool" },
                { "@azure/eslint-plugin-azure-sdk", "3.0.0 -> ./../../../../common/tools/eslint-plugin-azure-sdk" },
                { "@azure/identity", "2.1.0" },
                { "@azure/keyvault-keys", "4.2.0" },
                { "@azure/logger", "1.0.3, 1.0.4 -> ./../../../core/logger" },
                { "@azure/logger-js", "1.3.2" },
                { "@azure/msal-browser", "2.28.2" },
                { "@azure/msal-common", "7.4.0" },
                { "@azure/msal-node", "1.13.0" },
                { "@azure/storage-blob", "12.11.0" },
                { "@azure/test-utils-perf", "1.0.0 -> ./../../../test-utils/perf" },
                { "@azure-tools/test-recorder", "1.0.2" }
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
                { "@azure/core-auth", "1.4.1" },
                { "@azure/core-util", "1.1.1" },
                { "@azure/core-tracing", "1.0.0-preview.13, 1.0.2" },
                { "@azure/logger", "1.0.4" },
                { "@azure/core-rest-pipeline", "1.9.3 -> /mnt/vss/_work/1/s/sdk/core/core-rest-pipeline" },
                { "@azure/storage-blob", "12.11.0" },
                { "@azure/test-utils-perf", "1.0.0 -> /mnt/vss/_work/1/s/sdk/test-utils/perf" },
            };

            var actual = (new JavaScript()).FilterRuntimePackageVersions(allPackages);

            var expected = new Dictionary<string, string>() {
                { "@azure/core-http", "2.2.8 -> /mnt/vss/_work/1/s/sdk/core/core-http" },
                { "@azure/abort-controller", "1.1.1" },
                { "@azure/core-auth", "1.4.1" },
                { "@azure/core-util", "1.1.1" },
                { "@azure/core-tracing", "1.0.0-preview.13, 1.0.2" },
                { "@azure/logger", "1.0.4" },
                { "@azure/core-rest-pipeline", "1.9.3 -> /mnt/vss/_work/1/s/sdk/core/core-rest-pipeline" },
                { "@azure/storage-blob", "12.11.0" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }
    }
}
