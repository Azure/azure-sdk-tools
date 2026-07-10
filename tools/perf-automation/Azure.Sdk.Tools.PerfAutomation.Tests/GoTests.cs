using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    [TestFixture]
    public class GoTests
    {
        [Test]
        public void ParseOpsPerSecond_ValidOutput_ReturnsValue()
        {
            var output = "Completed 721 operations in a weighted-average of 30.00s (24.033 ops/s, 0.042 s/op)";
            var value = Go.ParseOpsPerSecond(output);

            Assert.That(value, Is.EqualTo(24.033).Within(0.0001));
        }

        [Test]
        public void ParseOpsPerSecond_CommaFormattedOutput_ReturnsValue()
        {
            var output = "Completed 1,234 operations in a weighted-average of 1.00s (12,557.81 ops/s, 0.000 s/op)";
            var value = Go.ParseOpsPerSecond(output);

            Assert.That(value, Is.EqualTo(12557.81).Within(0.0001));
        }

        [Test]
        public void ParseOpsPerSecond_MissingPattern_ReturnsMinusOne()
        {
            var output = "No throughput line present";
            var value = Go.ParseOpsPerSecond(output);

            Assert.That(value, Is.EqualTo(-1d));
        }

        [Test]
        public void ParseOpsPerSecond_NegativeValue_ReturnsMinusOne()
        {
            var output = "Completed 0 operations in a weighted-average of 30.00s (-24.033 ops/s, 0.042 s/op)";
            var value = Go.ParseOpsPerSecond(output);

            Assert.That(value, Is.EqualTo(-1d));
        }

        [Test]
        public void ParseOpsPerSecond_NullOrEmpty_ReturnsMinusOne()
        {
            Assert.That(Go.ParseOpsPerSecond(null), Is.EqualTo(-1d));
            Assert.That(Go.ParseOpsPerSecond(string.Empty), Is.EqualTo(-1d));
        }

        [Test]
        public void FilterRuntimePackageVersions_KeepsExpectedPackages()
        {
            var go = new Go();

            var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["github.com/Azure/azure-sdk-for-go/sdk/storage/azblob"] = "v1.6.4",
                ["golang.org/x/net"] = "v0.53.0",
                ["go"] = "go1.23.0",
                ["github.com/some/other"] = "v1.0.0"
            };

            var filtered = go.FilterRuntimePackageVersions(input);

            Assert.That(filtered, Is.Not.Null);
            Assert.That(filtered.ContainsKey("github.com/Azure/azure-sdk-for-go/sdk/storage/azblob"), Is.True);
            Assert.That(filtered.ContainsKey("golang.org/x/net"), Is.True);
            Assert.That(filtered.ContainsKey("go"), Is.True);
            Assert.That(filtered.ContainsKey("github.com/some/other"), Is.False);
        }

        [Test]
        public void FilterRuntimePackageVersions_NullInput_ReturnsEmptyDictionary()
        {
            var go = new Go();

            var filtered = go.FilterRuntimePackageVersions(null);

            Assert.That(filtered, Is.Not.Null);
            Assert.That(filtered, Is.Empty);
        }

        [Test]
        public void ResolveSourcePath_AzurePackage_ReturnsExpectedPath()
        {
            var go = new Go();
            go.WorkingDirectory = Path.Combine(Path.GetTempPath(), "azure-sdk-for-go");

            var result = go.ResolveSourcePath("github.com/Azure/azure-sdk-for-go/sdk/storage/azblob");
            var expected = Path.Combine(go.WorkingDirectory, "sdk", "storage", "azblob");

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ResolveSourcePath_NonAzurePackage_Throws()
        {
            var go = new Go();
            go.WorkingDirectory = Path.Combine(Path.GetTempPath(), "azure-sdk-for-go");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                go.ResolveSourcePath("github.com/not-azure/pkg"));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("Cannot resolve source path"));
        }
    }
}