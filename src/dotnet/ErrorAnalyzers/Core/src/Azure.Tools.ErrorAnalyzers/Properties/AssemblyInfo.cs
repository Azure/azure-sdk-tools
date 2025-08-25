// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

// Allow internal types to be visible to Client, General, and Management analyzer projects
[assembly: InternalsVisibleTo("Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers")]
[assembly: InternalsVisibleTo("Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers")]
[assembly: InternalsVisibleTo("Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers")]

// Allow internal types to be visible to test projects
[assembly: InternalsVisibleTo("Azure.Tools.ErrorAnalyzers.Tests")]
[assembly: InternalsVisibleTo("Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers.Tests")]
[assembly: InternalsVisibleTo("Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers.Tests")]
[assembly: InternalsVisibleTo("Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers.Tests")]

// Allow internal types to be visible to dynamic proxy generation (for mocking frameworks)
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

//TODO: Add signing key to AssemblyInfo.cs when strong naming is implemented
