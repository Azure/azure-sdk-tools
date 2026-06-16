// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Identifies which categories of source <c>azsdk_customized_code_update</c> is permitted to edit.
/// This is a flags enum, so categories can be combined.
/// </summary>
/// <remarks>
/// Regenerating <c>Generated/</c> from the unchanged pinned spec commit is always permitted and is
/// intentionally not represented here: generated code is never hand-edited — it is only re-emitted as
/// the deterministic result of a custom-code or spec-input change.
/// </remarks>
[Flags]
public enum EditScope
{
    /// <summary>No edits permitted.</summary>
    None = 0,

    /// <summary>
    /// Custom (non-generated) code: .NET partial classes / <c>[CodeGen*]</c> attributes,
    /// Python <c>_patch.py</c>, Java <c>*Customization.java</c>.
    /// </summary>
    CustomCode = 1 << 0,

    /// <summary>
    /// Spec inputs: <c>client.tsp</c> / <c>tspconfig.yaml</c> and the pinned spec commit in
    /// <c>tsp-location.yaml</c>. These live in the spec repo (<c>azure-rest-api-specs</c>); editing
    /// them, or moving the pinned commit, belongs in a separate spec-repo PR. When this flag is
    /// absent, any failure that would require a spec change is reported as out of scope
    /// (<see cref="Responses.Package.CustomizedCodeUpdateResponse.KnownErrorCodes.SpecChangeRequired"/>)
    /// instead of applied.
    /// </summary>
    SpecInputs = 1 << 1,

    /// <summary>
    /// Both custom code and spec inputs may be edited. Default for the local generate-SDK /
    /// API-review feedback flows.
    /// </summary>
    All = CustomCode | SpecInputs,
}
