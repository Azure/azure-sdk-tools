using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class NetTests
    {
        [Test]
        public void GetRuntimePackageVersions()
        {
            var standardOutput = File.ReadAllText("stdout/net.txt");
            var actual = Net.GetRuntimePackageVersions(standardOutput);

            var expected = new Dictionary<string, string>() {
                { "Azure.Core", "1.25.0+c8aaee521e662ddfb238d5ad1f2f9a79233f97f6" },
                { "Azure.Storage.Blobs", "12.13.0+dd17f33e411562517144e4b6b16f5ea910e5c5ae" },
                { "Azure.Storage.Blobs.Perf", "1.0.0-alpha.20220719.3+5e7750d5d3d4754b657da8430ea805591522c43b" },
                { "Azure.Storage.Common", "12.12.0+dd17f33e411562517144e4b6b16f5ea910e5c5ae" },
                { "Azure.Test.Perf", "1.0.0-alpha.20220719.3+5e7750d5d3d4754b657da8430ea805591522c43b" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }

        [Test]
        public void FilterRuntimePackageVersions()
        {
            var allPackages = new Dictionary<string, string>() {
                { "Azure.Core", "1.25.0+c8aaee521e662ddfb238d5ad1f2f9a79233f97f6" },
                { "Azure.Storage.Blobs", "12.13.0+dd17f33e411562517144e4b6b16f5ea910e5c5ae" },
                { "Azure.Storage.Blobs.Perf", "1.0.0-alpha.20220719.3+5e7750d5d3d4754b657da8430ea805591522c43b" },
                { "Azure.Storage.Common", "12.12.0+dd17f33e411562517144e4b6b16f5ea910e5c5ae" },
                { "Azure.Test.Perf", "1.0.0-alpha.20220719.3+5e7750d5d3d4754b657da8430ea805591522c43b" },
            };

            var actual = (new JavaScript()).FilterRuntimePackageVersions(allPackages);

            var expected = new Dictionary<string, string>() {
                { "Azure.Core", "1.25.0+c8aaee521e662ddfb238d5ad1f2f9a79233f97f6" },
                { "Azure.Storage.Blobs", "12.13.0+dd17f33e411562517144e4b6b16f5ea910e5c5ae" },
                { "Azure.Storage.Common", "12.12.0+dd17f33e411562517144e4b6b16f5ea910e5c5ae" },
            };

            CollectionAssert.AreEquivalent(expected, actual);
        }

    }
}
