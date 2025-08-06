// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Marks an analyzer class for automatic discovery.
    /// Used across all analyzer types (Client, General, Management).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class DiscoverableAnalyzerAttribute : Attribute
    {
    }
}
