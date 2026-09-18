// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Reports whether generation preparation was required and completed successfully.
/// </summary>
public sealed record GenerationPreparationResult(bool Required, bool Success, string? Diagnostics);
