using System.Collections.Generic;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class DictionaryEqualityComparerTests
    {
        private static readonly DictionaryEqualityComparer<string, string> _testComparer = new DictionaryEqualityComparer<string, string>();

        private static readonly Dictionary<string, string> k1v1 = new Dictionary<string, string>()
        {
            { "k1", "v1" },
        };

        private static readonly Dictionary<string, string> k1v1b = new Dictionary<string, string>()
        {
            { "k1", "v1" },
        };

        private static readonly Dictionary<string, string> k1v2 = new Dictionary<string, string>()
        {
            { "k1", "v2" },
        };

        private static readonly Dictionary<string, string> k2v1 = new Dictionary<string, string>()
        {
            { "k2", "v1" },
        };

        private static readonly Dictionary<string, string> k1v1k2v2 = new Dictionary<string, string>()
        {
            { "k1", "v1" },
            { "k2", "v2" },
        };

        private static readonly Dictionary<string, string> k1v1k2v2b = new Dictionary<string, string>()
        {
            { "k1", "v1" },
            { "k2", "v2" },
        };

        private static readonly Dictionary<string, string> k1v1k2v3 = new Dictionary<string, string>()
        {
            { "k1", "v1" },
            { "k2", "v3" },
        };

        [Test]
        public void Null()
        {
            Assert.True(_testComparer.Equals(null, null));
            Assert.False(_testComparer.Equals(null, new Dictionary<string, string>()));
            Assert.False(_testComparer.Equals(new Dictionary<string, string>(), null));

            Assert.AreEqual(0, _testComparer.GetHashCode(null));
        }

        [Test]
        public void Strings()
        {
            // Same dictionary
            Assert.True(_testComparer.Equals(k1v1, k1v1));
            Assert.True(_testComparer.Equals(k1v1k2v2, k1v1k2v2));

            // Identical dictionary
            Assert.True(_testComparer.Equals(k1v1, k1v1b));
            Assert.True(_testComparer.Equals(k1v1k2v2, k1v1k2v2b));

            // Different key
            Assert.False(_testComparer.Equals(k1v1, k1v2));

            // Different value
            Assert.False(_testComparer.Equals(k1v1, k2v1));
            Assert.False(_testComparer.Equals(k1v1k2v2, k1v1k2v3));

            // Different length
            Assert.False(_testComparer.Equals(k1v1, k1v1k2v2));
        }
    }
}
