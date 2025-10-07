// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.Languages
{
    /// <summary>
    /// Interface for language-specific type checkers.
    /// </summary>
    internal interface ILanguageTypechecker
    {
        /// <summary>
        /// Performs type checking on the provided code.
        /// </summary>
        /// <param name="parameters">Type check parameters</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Type check result</returns>
        Task<TypeCheckResult> TypecheckAsync(TypeCheckRequest parameters, CancellationToken ct);
    }
}