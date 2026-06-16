// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Controls the behavior of <c>azsdk_customized_code_update</c>.
/// </summary>
public enum CustomizedCodeUpdateMode
{
    /// <summary>
    /// Default, interactive/local behavior: may apply TypeSpec (spec-input) customizations
    /// such as <c>client.tsp</c> decorators, regenerate, and patch custom code.
    /// </summary>
    Update,

    /// <summary>
    /// Headless, custom-code-only repair of an already-generated SDK PR whose build fails
    /// because of custom (non-generated) code drift. In this mode the tool must never edit
    /// spec inputs (<c>client.tsp</c> / <c>tspconfig.yaml</c>) or move the pinned spec commit:
    /// any failure that would require a spec change is reported as out of scope
    /// (<see cref="Responses.Package.CustomizedCodeUpdateResponse.KnownErrorCodes.SpecChangeRequired"/>)
    /// instead of applied. Regenerating <c>Generated/</c> from the unchanged pinned commit is allowed.
    /// </summary>
    Repair
}
