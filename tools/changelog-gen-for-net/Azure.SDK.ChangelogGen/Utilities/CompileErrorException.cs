// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis.Emit;

namespace Azure.SDK.ChangelogGen.Utilities
{
    internal class CompileErrorException : Exception
    {
        public EmitResult EmitResult { get; private set; }

        public CompileErrorException(EmitResult emitResult)
            : base(string.Join("\n", emitResult.Diagnostics.Select(d => d.GetMessage())))
        {
            this.EmitResult = emitResult;
        }
    }
}
