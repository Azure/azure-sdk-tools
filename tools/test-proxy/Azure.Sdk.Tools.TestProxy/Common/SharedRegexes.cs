// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    /// <summary>
    /// A static class containing shared regular expressions used across the test proxy.
    /// </summary>
    public static partial class SharedRegexes
    {
        [GeneratedRegex(".+", RegexOptions.Compiled)]
        public static partial Regex DotAll();

        [GeneratedRegex("/oauth2(?:/v2.0)?/token", RegexOptions.Compiled)]
        public static partial Regex OAuth2Token();

        [GeneratedRegex("SharedAccessKey=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        public static partial Regex SharedAccessKey();

        [GeneratedRegex("AccountKey=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        public static partial Regex AccountKey();

        [GeneratedRegex("accesskey=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        public static partial Regex AccessKey();

        [GeneratedRegex("Accesskey=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        public static partial Regex AccessKey2();

        [GeneratedRegex("Secret=(?<key>[^;\\\"]+)", RegexOptions.Compiled)]
        public static partial Regex Secret();

        [GeneratedRegex("common/userrealm/(?<realm>[^/\\.]+)", RegexOptions.Compiled)]
        public static partial Regex UserRealm();

        [GeneratedRegex("/identities/(?<realm>[^/?]+)", RegexOptions.Compiled)]
        public static partial Regex Identities();

        [GeneratedRegex("(?:[?&](sig|sv)=)(?<secret>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        public static partial Regex SasToken();

        [GeneratedRegex("token=(?<token>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        public static partial Regex Token();

        [GeneratedRegex("(client_id=)(?<cid>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        public static partial Regex ClientId();

        [GeneratedRegex("client_secret=(?<secret>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        public static partial Regex ClientSecret();

        [GeneratedRegex("client_assertion=(?<secret>[^&\\\"\\s\\n,\\\\]+)", RegexOptions.Compiled)]
        public static partial Regex ClientAssertion();

        [GeneratedRegex("-----BEGIN PRIVATE KEY-----\\n(?<cert>.+\\n)*-----END PRIVATE KEY-----\\n", RegexOptions.Compiled)]
        public static partial Regex PrivateKey();

        [GeneratedRegex("(?<=<UserDelegationKey>).+?(?:<Value>)(?<group>.+)(?:</Value>)", RegexOptions.Compiled)]
        public static partial Regex UserDelegationKeyValue();

        [GeneratedRegex("(?<=<UserDelegationKey>).+?(?:<SignedTid>)(?<group>.+)(?:</SignedTid>)", RegexOptions.Compiled)]
        public static partial Regex UserDelegationKeySignedTid();

        [GeneratedRegex("(?<=<UserDelegationKey>).+?(?:<SignedOid>)(?<group>.+)(?:</SignedOid>)", RegexOptions.Compiled)]
        public static partial Regex UserDelegationKeySignedOid();

        [GeneratedRegex("(?:Password=)(?<pwd>.+?)(?:;)", RegexOptions.Compiled)]
        public static partial Regex Password();

        [GeneratedRegex("(?:User ID=)(?<id>.+?)(?:;)", RegexOptions.Compiled)]
        public static partial Regex UserId();

        [GeneratedRegex("(?:<PrimaryKey>)(?<key>.+)(?:</PrimaryKey>)", RegexOptions.Compiled)]
        public static partial Regex PrimaryKey();

        [GeneratedRegex("(?:<SecondaryKey>)(?<key>.+)(?:</SecondaryKey>)", RegexOptions.Compiled)]
        public static partial Regex SecondaryKey();

        [GeneratedRegex("<ClientIp>(?<secret>.+)</ClientIp>", RegexOptions.Compiled)]
        public static partial Regex ClientIp();

        [GeneratedRegex("sig=(?<sig>[^&]+)", RegexOptions.Compiled)]
        public static partial Regex Sig();

        [GeneratedRegex("(?<=http://|https://)(?<host>[^/?\\.]+)", RegexOptions.Compiled)]
        public static partial Regex Host();
    }
}
