using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class PythonTests
    {
        [Test]
        public void GetRuntimePackageVersions()
        {
            var standardOutput = File.ReadAllText("stdout/python.txt");
            var actual = Python.GetRuntimePackageVersions(standardOutput);

            var expected = new Dictionary<string, string>() {
                { "azure-common", "1.1.28" },
                { "azure-core", "1.25.0" },
                { "azure-devtools", "1.2.1 -> /mnt/vss/_work/1/s/tools/azure-devtools/src" },
                { "azure-identity", "1.10.0" },
                { "azure-mgmt-core", "1.3.1" },
                { "azure-mgmt-keyvault", "10.0.0" },
                { "azure-mgmt-resource", "21.1.0" },
                { "azure-mgmt-storage", "20.0.0 -> /mnt/vss/_work/1/s/sdk/storage/azure-mgmt-storage" },
                { "azure-sdk-tools", "0.0.0 -> /mnt/vss/_work/1/s/tools/azure-sdk-tools" },
                { "azure-storage-blob", "12.14.0b1 -> /mnt/vss/_work/1/s/sdk/storage/azure-storage-blob" },
                { "azure-storage-common", "1.4.0" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }

        [Test]
        public void FilterRuntimePackageVersions()
        {
            var allPackages = new Dictionary<string, string>() {
                { "azure-common", "1.1.28" },
                { "azure-core", "1.25.0" },
                { "azure-devtools", "1.2.1 -> /mnt/vss/_work/1/s/tools/azure-devtools/src" },
                { "azure-identity", "1.10.0" },
                { "azure-mgmt-core", "1.3.1" },
                { "azure-mgmt-keyvault", "10.0.0" },
                { "azure-mgmt-resource", "21.1.0" },
                { "azure-mgmt-storage", "20.0.0 -> /mnt/vss/_work/1/s/sdk/storage/azure-mgmt-storage" },
                { "azure-sdk-tools", "0.0.0 -> /mnt/vss/_work/1/s/tools/azure-sdk-tools" },
                { "azure-storage-blob", "12.14.0b1 -> /mnt/vss/_work/1/s/sdk/storage/azure-storage-blob" },
                { "azure-storage-common", "1.4.0" },
            };

            var actual = (new Python()).FilterRuntimePackageVersions(allPackages);

            var expected = new Dictionary<string, string>() {
                { "azure-core", "1.25.0" },
                { "azure-identity", "1.10.0" },
                { "azure-storage-blob", "12.14.0b1 -> /mnt/vss/_work/1/s/sdk/storage/azure-storage-blob" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }
    }
}
