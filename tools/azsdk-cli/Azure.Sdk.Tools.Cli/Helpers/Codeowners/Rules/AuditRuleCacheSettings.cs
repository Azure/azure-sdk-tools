// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

internal static class AuditRuleCacheSettings
{
    internal static TimeSpan CacheMaxAge { get; } = TimeSpan.FromHours(6);
}
