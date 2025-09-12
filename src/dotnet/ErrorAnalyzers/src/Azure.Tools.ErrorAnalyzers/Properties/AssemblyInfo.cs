// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;


// Allow internal types to be visible to test projects
[assembly: InternalsVisibleTo("Azure.Tools.ErrorAnalyzers.Tests")]


// Allow internal types to be visible to dynamic proxy generation (for mocking frameworks)
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

//TODO: Add signing key to AssemblyInfo.cs when strong naming is implemented
