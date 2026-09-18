using System;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    internal static partial class KnownSanitizerRegexes
    {
        private static readonly FrozenDictionary<string, Regex> s_patterns = new[]
        {
            WholeValue(), SharedAccessKey(), AccountKey(), LowerAccessKey(), MixedAccessKey(), Secret(),
            UserRealm(), Identity(), SasQuery(), Token(), ClientId(), ClientSecret(), ClientAssertion(),
            PrivateKey(), Password(), UserId(), Signature(), Host()
        }.ToFrozenDictionary(regex => regex.ToString(), StringComparer.Ordinal);

        internal static Regex Get(string pattern)
        {
            return pattern != null && s_patterns.TryGetValue(pattern, out var regex) ? regex : null;
        }

        [GeneratedRegex(".+", RegexOptions.Compiled)]
        private static partial Regex WholeValue();

        [GeneratedRegex("SharedAccessKey=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        private static partial Regex SharedAccessKey();

        [GeneratedRegex("AccountKey=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        private static partial Regex AccountKey();

        [GeneratedRegex("accesskey=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        private static partial Regex LowerAccessKey();

        [GeneratedRegex("Accesskey=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        private static partial Regex MixedAccessKey();

        [GeneratedRegex("Secret=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        private static partial Regex Secret();

        [GeneratedRegex("common/userrealm/(?<realm>[^/\\.]+)", RegexOptions.Compiled)]
        private static partial Regex UserRealm();

        [GeneratedRegex("/identities/(?<realm>[^/?]+)", RegexOptions.Compiled)]
        private static partial Regex Identity();

        [GeneratedRegex("(?:[?&](sig|sv)=)(?<secret>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        private static partial Regex SasQuery();

        [GeneratedRegex("token=(?<token>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        private static partial Regex Token();

        [GeneratedRegex("(client_id=)(?<cid>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        private static partial Regex ClientId();

        [GeneratedRegex("client_secret=(?<secret>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        private static partial Regex ClientSecret();

        [GeneratedRegex("client_assertion=(?<secret>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        private static partial Regex ClientAssertion();

        [GeneratedRegex("-----BEGIN PRIVATE KEY-----\\n(?<cert>.+\\n)*-----END PRIVATE KEY-----\\n", RegexOptions.Compiled)]
        private static partial Regex PrivateKey();

        [GeneratedRegex("(?:Password=)(?<pwd>.+?)(?:;)", RegexOptions.Compiled)]
        private static partial Regex Password();

        [GeneratedRegex("(?:User ID=)(?<id>.+?)(?:;)", RegexOptions.Compiled)]
        private static partial Regex UserId();

        [GeneratedRegex("sig=(?<sig>[^&]+)", RegexOptions.Compiled)]
        private static partial Regex Signature();

        [GeneratedRegex("(?<=http://|https://)(?<host>[^/?\\.]+)", RegexOptions.Compiled)]
        private static partial Regex Host();
    }
}
