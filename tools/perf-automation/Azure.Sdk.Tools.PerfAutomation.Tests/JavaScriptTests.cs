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

            var expected = new Dictionary<string, string>() {
                { "@azure-tests/perf-storage-blob", "1.0.0 /mnt/vss/_work/1/s/sdk/storage/perf-tests/storage-blob" },
                { "@azure/core-http", "2.2.6 -> /mnt/vss/_work/1/s/sdk/core/core-http" },
                { "@azure/core-rest-pipeline", "1.9.1 -> /mnt/vss/_work/1/s/sdk/core/core-rest-pipeline" },
                { "@azure/storage-blob", "12.11.0 -> /mnt/vss/_work/1/s/common/temp/node_modules/.pnpm/@azure+storage-blob@12.11.0/node_modules/@azure/storage-blob" },
                { "@azure/test-utils-perf", "1.0.0 -> /mnt/vss/_work/1/s/sdk/test-utils/perf" },
            };

            var actual = JavaScript.GetRuntimePackageVersions(standardOutput);

            CollectionAssert.AreEquivalent(expected, actual);
        }
    }
}
