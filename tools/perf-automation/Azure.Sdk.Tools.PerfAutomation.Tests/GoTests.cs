using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using System.Reflection;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    [TestFixture]
    public class GoTests
    {
        [Test]
        public void ParseOpsPerSecond_ValidOutput_ReturnsValue()
        {
            var output = "Completed 721 operations in a weighted-average of 30.00s (24.033 ops/s, 0.042 s/op)";
            var value = InvokePrivateStaticDouble("ParseOpsPerSecond", output);

            Assert.That(value, Is.EqualTo(24.033).Within(0.0001));
        }

        [Test]
        public void ParseOpsPerSecond_CommaFormattedOutput_ReturnsValue()
        {
            var output = "Completed 1,234 operations in a weighted-average of 1.00s (12,557.81 ops/s, 0.000 s/op)";
            var value = InvokePrivateStaticDouble("ParseOpsPerSecond", output);

            Assert.That(value, Is.EqualTo(12557.81).Within(0.0001));
        }

        [Test]
        public void ParseOpsPerSecond_MissingPattern_ReturnsMinusOne()
        {
            var output = "No throughput line present";
            var value = InvokePrivateStaticDouble("ParseOpsPerSecond", output);

            Assert.That(value, Is.EqualTo(-1d));
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
        public void ResolveSourcePath_AzurePackage_ReturnsExpectedPath()
        {
            var go = new Go();
            go.WorkingDirectory = Path.Combine(Path.DirectorySeparatorChar.ToString(), "tmp", "azure-sdk-for-go");

            var result = InvokePrivateInstanceString(go, "ResolveSourcePath", "github.com/Azure/azure-sdk-for-go/sdk/storage/azblob");
            var expected = Path.Combine(go.WorkingDirectory, "sdk", "storage", "azblob");

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ResolveSourcePath_NonAzurePackage_Throws()
        {
            var go = new Go();
            go.WorkingDirectory = Path.Combine(Path.DirectorySeparatorChar.ToString(), "tmp", "azure-sdk-for-go");

            var ex = Assert.Throws<TargetInvocationException>(() =>
                InvokePrivateInstanceString(go, "ResolveSourcePath", "github.com/not-azure/pkg"));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(ex.InnerException?.Message, Does.Contain("Cannot resolve source path"));
        }

        private static double InvokePrivateStaticDouble(string methodName, string arg)
        {
            var method = typeof(Go).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, $"Method {methodName} not found");

            var result = method!.Invoke(null, new object[] { arg });
            Assert.That(result, Is.TypeOf<double>());

            return (double)result!;
        }

        private static string InvokePrivateInstanceString(object instance, string methodName, string arg)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, $"Method {methodName} not found");

            var result = method!.Invoke(instance, new object[] { arg });
            Assert.That(result, Is.TypeOf<string>());

            return (string)result!;
        }
    }
}