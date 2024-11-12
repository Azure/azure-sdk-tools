using System;
using Octokit;

namespace Azure.Sdk.LabelTrainer
{
    internal static class AzureSdkLabel
    {
        public static bool IsServiceLabel(Label label) =>
            string.Equals(label.Color, "e99695", StringComparison.InvariantCultureIgnoreCase);

        public static bool IsCategoryLabel(Label label) =>
            string.Equals(label.Color, "ffeb77", StringComparison.InvariantCultureIgnoreCase);
    }
}
