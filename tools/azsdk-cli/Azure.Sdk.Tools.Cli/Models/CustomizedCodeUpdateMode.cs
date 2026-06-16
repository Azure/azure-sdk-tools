// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Controls the behavior of <c>azsdk_customized_code_update</c>. The tool is parameter-driven
/// and non-interactive in every mode; the mode selects the <b>scope of edits</b> the tool is
/// permitted to make.
/// </summary>
public enum CustomizedCodeUpdateMode
{
    /// <summary>
    /// Default behavior: edits are <b>unrestricted</b>. The tool may apply spec-input
    /// (TypeSpec) customizations such as <c>client.tsp</c> decorators, regenerate, and patch
    /// custom (non-generated) code. Used by the local generate-SDK / API-review feedback flows.
    /// </summary>
    Update,

    /// <summary>
    /// Custom-code-only repair of an already-generated SDK PR whose build fails because of
    /// custom (non-generated) code drift. Edits are restricted to custom code: the tool must
    /// never edit spec inputs (<c>client.tsp</c> / <c>tspconfig.yaml</c>) or move the pinned
    /// spec commit. Any failure that would require a spec change is reported as out of scope
    /// (<see cref="Responses.Package.CustomizedCodeUpdateResponse.KnownErrorCodes.SpecChangeRequired"/>)
    /// instead of applied. Regenerating <c>Generated/</c> from the unchanged pinned commit is allowed.
    /// </summary>
    Repair
}
