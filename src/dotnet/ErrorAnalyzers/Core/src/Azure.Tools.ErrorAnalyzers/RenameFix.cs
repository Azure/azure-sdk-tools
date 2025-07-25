// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// A fix that renames a code element.
    /// </summary>
    public sealed class RenameFix : Fix
    {
        public RenameFix(string originalName, string newName) 
            : base(FixAction.Rename)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(originalName);
            ArgumentException.ThrowIfNullOrWhiteSpace(newName);
            OriginalName = originalName;
            NewName = newName;
        }

        /// <summary>
        /// The current name to be renamed.
        /// </summary>
        public string OriginalName { get; }

        /// <summary>
        /// The new name to rename to.
        /// </summary>
        public string NewName { get; }
    }
}
