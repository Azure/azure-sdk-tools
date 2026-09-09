using Azure.Sdk.Tools.CodeownersMigration.Conversion;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersMigration.Tests
{
    public class FragmentPrefixTests
    {
        // A trailing '/' is what restricts a CODEOWNERS expression to a directory, so it has to survive
        // the round trip. The last case is the same name without it, which must stay a file match.
        [TestCase("/sdk/ai/", "sdk/ai", ".")]
        [TestCase("/sdk/ai/Azure.AI.Inference/", "sdk/ai", "Azure.AI.Inference/")]
        [TestCase("/sdk/storage/Azure.Storage.Blobs/src/", "sdk/storage", "Azure.Storage.Blobs/src/")]
        [TestCase("/sdk/apicenter/Azure.ResourceManager.*/", "sdk/apicenter", "Azure.ResourceManager.*/")]
        [TestCase("/sdk/ai/Azure.AI.Inference", "sdk/ai", "Azure.AI.Inference")]
        public void AttributesPathsUnderTheGlob(string path, string expectedRoot, string expectedRelative)
        {
            var prefix = FragmentPrefix.Parse("sdk/*/owners.yaml");

            Assert.That(prefix.RootFor(path), Is.EqualTo(expectedRoot));
            Assert.That(prefix.RelativePath(expectedRoot, path), Is.EqualTo(expectedRelative));
        }

        // A fragment has to live in a real directory, so a glob in the segment that would become the
        // fragment's location makes the entry unattributable.
        [TestCase("/sdk/")]
        [TestCase("/sdk/iot*/")]
        [TestCase("/sdk/**/ci.mgmt.yml")]
        [TestCase("/**/Azure.ResourceManager*/")]
        [TestCase("/eng/common/")]
        [TestCase("/*")]
        public void RejectsPathsThatCannotBecomeFragments(string path)
        {
            var prefix = FragmentPrefix.Parse("sdk/*/owners.yaml");

            Assert.That(prefix.RootFor(path), Is.Null);
        }

        [Test]
        public void SupportsDeeperGlobs()
        {
            var prefix = FragmentPrefix.Parse("sdk/resourcemanager/*/owners.yaml");

            Assert.That(prefix.RootFor("/sdk/resourcemanager/compute/armcompute/"), Is.EqualTo("sdk/resourcemanager/compute"));
            Assert.That(prefix.RelativePath("sdk/resourcemanager/compute", "/sdk/resourcemanager/compute/armcompute/"), Is.EqualTo("armcompute/"));
            Assert.That(prefix.RootFor("/sdk/ai/Azure.AI.Inference/"), Is.Null);
        }

        [Test]
        public void AllowedGlobsIncludesYmlVariant()
        {
            Assert.That(FragmentPrefix.AllowedGlobs("sdk/*/owners.yaml"),
                        Is.EqualTo(new[] { "sdk/*/owners.yaml", "sdk/*/owners.yml" }));
        }
    }
}
