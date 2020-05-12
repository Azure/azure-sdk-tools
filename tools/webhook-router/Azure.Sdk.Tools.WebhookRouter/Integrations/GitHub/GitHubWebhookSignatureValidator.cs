using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.WebhookRouter.Integrations.GitHub
{
    public static class GitHubWebhookSignatureValidator
    {
        public const string GitHubWebhookSignatureHeader = "X-Hub-Signature";

        public static bool IsValid(byte[] body, string signature, string secret)
        {
            var key = Encoding.UTF8.GetBytes(secret);
            var sha1 = new HMACSHA1(key);

            var digest = sha1.ComputeHash(body);
            var hex = BitConverter.ToString(digest).Replace("-", "").ToLower();
            var generatedSignature = $"sha1={hex}";

            return signature == generatedSignature;
        }
    }
}
