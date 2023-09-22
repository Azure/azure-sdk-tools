using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class JavaTests
    {
        [Test]
        public void GetRuntimePackageVersions()
        {
            var standardOutput = File.ReadAllText("stdout/java.txt");
            var actual = Java.GetRuntimePackageVersions(standardOutput);

            var expected = new Dictionary<string, string>() {
                { "com.azure:azure-core-http-okhttp", "1.11.2" },
                { "com.azure:azure-storage-blob-cryptography", "12.19.0-beta.2" },
                { "com.azure:azure-core", "1.32.0" },
                { "com.azure:azure-core-http-netty", "1.12.5" },
                { "com.azure:azure-storage-internal-avro", "12.5.0-beta.2" },
                { "com.azure:azure-storage-blob", "12.20.0-beta.2" },
                { "com.azure:azure-security-keyvault-keys", "4.4.6" },
                { "com.azure:perf-test-core", "1.0.0-beta.1" },
                { "com.azure:azure-storage-file-share", "12.16.0-beta.2" },
                { "com.azure:azure-storage-common", "12.19.0-beta.2" },
                { "com.azure:azure-storage-file-datalake", "12.13.0-beta.2" },
                { "io.projectreactor:reactor-core", "3.4.22" },
                { "io.projectreactor.netty:reactor-netty-core", "1.0.22" },
                { "io.projectreactor.netty:reactor-netty-http", "1.0.22" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }

        [Test]
        public void FilterRuntimePackageVersions()
        {
            var allPackages = new Dictionary<string, string>() {
                { "com.azure:azure-core-http-okhttp", "1.11.2" },
                { "com.azure:azure-storage-blob-cryptography", "12.19.0-beta.2" },
                { "com.azure:azure-core", "1.32.0" },
                { "com.azure:azure-core-http-netty", "1.12.5" },
                { "com.azure:azure-storage-internal-avro", "12.5.0-beta.2" },
                { "com.azure:azure-storage-blob", "12.20.0-beta.2" },
                { "com.azure:azure-security-keyvault-keys", "4.4.6" },
                { "com.azure:perf-test-core", "1.0.0-beta.1" },
                { "com.azure:azure-storage-file-share", "12.16.0-beta.2" },
                { "com.azure:azure-storage-common", "12.19.0-beta.2" },
                { "com.azure:azure-storage-file-datalake", "12.13.0-beta.2" },
                { "io.projectreactor:reactor-core", "3.4.22" },
                { "io.projectreactor.netty:reactor-netty-core", "1.0.22" },
                { "io.projectreactor.netty:reactor-netty-http", "1.0.22" },
            };

            var actual = (new Java()).FilterRuntimePackageVersions(allPackages);

            var expected = new Dictionary<string, string>() {
                { "com.azure:azure-core-http-okhttp", "1.11.2" },
                { "com.azure:azure-storage-blob-cryptography", "12.19.0-beta.2" },
                { "com.azure:azure-core", "1.32.0" },
                { "com.azure:azure-core-http-netty", "1.12.5" },
                { "com.azure:azure-storage-internal-avro", "12.5.0-beta.2" },
                { "com.azure:azure-storage-blob", "12.20.0-beta.2" },
                { "com.azure:azure-security-keyvault-keys", "4.4.6" },
                { "com.azure:azure-storage-file-share", "12.16.0-beta.2" },
                { "com.azure:azure-storage-common", "12.19.0-beta.2" },
                { "com.azure:azure-storage-file-datalake", "12.13.0-beta.2" },
                { "io.projectreactor:reactor-core", "3.4.22" },
                { "io.projectreactor.netty:reactor-netty-core", "1.0.22" },
                { "io.projectreactor.netty:reactor-netty-http", "1.0.22" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }
    }
}
