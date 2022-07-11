using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Azure.Sdk.Tools.PerfAutomation
{
    internal class DictionaryEqualityComparer<K, V> : IEqualityComparer<IDictionary<K, V>>
    {
        public bool Equals(IDictionary<K, V> x, IDictionary<K, V> y)
        {
            return x.Count == y.Count && !x.Except(y).Any();
        }

        public int GetHashCode([DisallowNull] IDictionary<K, V> obj)
        {
            var hashCode = new HashCode();
            foreach (var kvp in obj)
            {
                hashCode.Add(kvp.Key);
                hashCode.Add(kvp.Value);
            }
            return hashCode.ToHashCode();
        }
    }
}
