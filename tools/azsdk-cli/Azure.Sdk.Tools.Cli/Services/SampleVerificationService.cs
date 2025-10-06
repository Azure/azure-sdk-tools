// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.SampleGeneration;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services
{
    /// <summary>
    /// Parameters for type checking a code sample.
    /// </summary>
    /// <param name="Code">The source code to verify</param>
    /// <param name="ClientDist">Optional path to client distribution package</param>
    /// <param name="PackageName">Optional package name to exclude from auto-discovery</param>
    public record TypeCheckRequest(
        string Code,
        string? ClientDist,
        string? PackageName
    );

    /// <summary>
    /// Result of a type checking operation.
    /// </summary>
    /// <param name="Succeeded">Whether the type check passed</param>
    /// <param name="Output">Output from the type checker (errors, warnings, etc.)</param>
    public record TypeCheckResult(
        bool Succeeded,
        string Output
    );

    /// <summary>
    /// Represents a single verification attempt.
    /// </summary>
    /// <param name="AttemptNumber">The attempt number (1-based)</param>
    /// <param name="TypeCheckSucceeded">Whether the type check passed</param>
    /// <param name="TypeCheckOutput">Output from the type checker</param>
    /// <param name="Duration">Time taken for this attempt</param>
    public record VerificationAttempt(
        int AttemptNumber,
        bool TypeCheckSucceeded,
        string TypeCheckOutput,
        TimeSpan Duration
    );

    /// <summary>
    /// Result of the complete verification process.
    /// </summary>
    /// <param name="Succeeded">Whether verification ultimately succeeded</param>
    /// <param name="Content">The final code content (may be fixed)</param>
    /// <param name="AttemptsMade">Number of attempts made</param>
    /// <param name="Attempts">Details of each attempt</param>
    public record VerificationResult(
        bool Succeeded,
        string Content,
        int AttemptsMade,
        List<VerificationAttempt> Attempts
    );

    /// <summary>
    /// Utility class for verifying and fixing code samples using containerized type checkers.
    /// </summary>
    public static class SampleVerification
    {
        private const int MaxAttempts = 5;

        /// <summary>
        /// Verifies a sample and attempts to fix it using AI if verification fails.
        /// </summary>
        public static async Task<VerificationResult> VerifyAndFixSampleAsync(
            GeneratedSample sample,
            string language,
            IDockerService dockerService,
            IMicroagentHostService microagentService,
            ILogger logger,
            string? clientDist = null,
            string? packageName = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(sample);
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("Language cannot be null or empty", nameof(language));
            }

            logger.LogInformation("Starting verification for sample: {fileName} (language: {language})",
                sample.FileName, language);

            // Check if Docker is available
            if (!await dockerService.IsDockerAvailableAsync(ct))
            {
                logger.LogError("Docker is not available. Verification requires Docker to be installed and running.");
                return new VerificationResult(
                    false,
                    sample.Content,
                    0,
                    [
                        new(1, false, "Docker is not available. Please install and start Docker to use verification.", TimeSpan.Zero)
                    ]);
            }

            // Get the appropriate typechecker
            ILanguageTypechecker typechecker;
            try
            {
                typechecker = LanguageSupport.CreateTypechecker(language, dockerService, logger);
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Language {language} is not supported for verification", language);
                return new VerificationResult(
                    false,
                    sample.Content,
                    0,
                    [
                        new(1, false, $"Language '{language}' is not supported for verification: {ex.Message}", TimeSpan.Zero)
                    ]);
            }

            var attempts = new List<VerificationAttempt>();
            var content = sample.Content;

            // Verification loop with retry and AI-based fixing
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                logger.LogDebug("Verification attempt {attempt}/{maxAttempts} for {fileName}",
                    attempt, MaxAttempts, sample.FileName);

                var startTime = DateTime.UtcNow;

                try
                {
                    var typeCheckResult = await typechecker.TypecheckAsync(
                        new TypeCheckRequest(content, clientDist, packageName), ct);

                    var duration = DateTime.UtcNow - startTime;
                    attempts.Add(new VerificationAttempt(attempt, typeCheckResult.Succeeded, typeCheckResult.Output, duration));

                    if (typeCheckResult.Succeeded)
                    {
                        logger.LogInformation("Sample verification succeeded on attempt {attempt} for {fileName}",
                            attempt, sample.FileName);
                        return new VerificationResult(true, content, attempt, attempts);
                    }

                    logger.LogDebug("Attempt {attempt} failed for {fileName}: {output}",
                        attempt, sample.FileName, typeCheckResult.Output);

                    // If this isn't the last attempt, try to fix the code using AI
                    if (attempt < MaxAttempts)
                    {
                        logger.LogDebug("Attempting to fix code using AI for {fileName}", sample.FileName);
                        content = await FixCodeWithAI(content, typeCheckResult.Output, language, microagentService, logger, ct);
                        logger.LogDebug("AI provided fixed code for {fileName}", sample.FileName);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during verification attempt {attempt} for {fileName}",
                        attempt, sample.FileName);

                    var duration = DateTime.UtcNow - startTime;
                    attempts.Add(new VerificationAttempt(attempt, false, $"Error during verification: {ex.Message}", duration));

                    // Don't attempt to fix on errors, just log and continue or fail
                    if (attempt == MaxAttempts)
                    {
                        break;
                    }
                }
            }

            logger.LogWarning("Sample verification failed after {maxAttempts} attempts for {fileName}",
                MaxAttempts, sample.FileName);

            return new VerificationResult(false, content, MaxAttempts, attempts);
        }


        /// <summary>
        /// Uses AI to fix code based on type checker errors.
        /// </summary>
        private static async Task<string> FixCodeWithAI(
            string code,
            string errors,
            string language,
            IMicroagentHostService microagentService,
            ILogger logger,
            CancellationToken ct)
        {
            var instructions = $@"
You are an expert {language} developer and code reviewer.
Your task is to fix the following issues in the sample code:

{errors}

Original code:
{code}

Respond with the corrected {language} code only. No markdown, no explanations, no code fences, no XML tags, no string delimiters wrapping it, and no extra text.

{LanguageSupport.GetTypecheckingInstructions(language)}";
            logger.LogDebug(instructions);
            logger.LogDebug("Sending code to AI for fixing");

            var microagent = new Microagent<string>()
            {
                Instructions = instructions
            };

            var fixedCode = await microagentService.RunAgentToCompletion(microagent, ct);

            if (string.IsNullOrEmpty(fixedCode))
            {
                throw new InvalidOperationException("AI failed to return fixed code");
            }

            logger.LogDebug("AI returned fixed code (length: {length})", fixedCode.Length);

            return fixedCode;
        }
    }
}