// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// A <c>CFG-*</c> rule violation found while turning the ownership YAML into a CODEOWNERS file.
/// Any error aborts generation; nothing is written.
/// </summary>
/// <param name="Code">The rule that rejected the input, e.g. <c>CFG-PATH-001</c>.</param>
/// <param name="Message">
/// What is wrong and where. Multi-line for rules that implicate more than one file, in which case
/// continuation lines are indented two spaces.
/// </param>
public sealed record OwnersValidationError(string Code, string Message)
{
    public override string ToString() => $"{Code}: {Message}";
}
