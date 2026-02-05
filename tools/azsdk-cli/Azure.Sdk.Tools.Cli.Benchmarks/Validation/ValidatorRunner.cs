// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation;

/// <summary>
/// Runs validators and aggregates results.
/// </summary>
public class ValidatorRunner
{
    /// <summary>
    /// Options for controlling validator execution.
    /// </summary>
    public class Options
    {
        /// <summary>
        /// Gets or sets whether to stop on first failure.
        /// Default: false (run all validators).
        /// </summary>
        public bool StopOnFirstFailure { get; init; } = false;

        /// <summary>
        /// Gets or sets the default timeout for validators.
        /// </summary>
        public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromMinutes(5);
    }

    private readonly Options _options;

    public ValidatorRunner(Options? options = null)
    {
        _options = options ?? new Options();
    }

    /// <summary>
    /// Runs all validators and returns aggregated results.
    /// </summary>
    /// <param name="validators">The validators to run.</param>
    /// <param name="context">The validation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated validation results.</returns>
    public async Task<ValidationSummary> RunAsync(
        IEnumerable<IValidator> validators,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ValidationResult>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (var validator in validators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine($"  Running validator: {validator.Name}...");

            try
            {
                var result = await validator.ValidateAsync(context, cancellationToken);
                results.Add(result);

                var status = result.Passed ? "✓" : "✗";
                Console.WriteLine($"    {status} {result.Message ?? (result.Passed ? "Passed" : "Failed")}");

                if (!result.Passed && _options.StopOnFirstFailure)
                {
                    Console.WriteLine("  Stopping on first failure");
                    break;
                }
            }
            catch (Exception ex)
            {
                var result = ValidationResult.Fail(validator.Name, $"Validator threw exception: {ex.Message}");
                results.Add(result);

                Console.WriteLine($"    ✗ Exception: {ex.Message}");

                if (_options.StopOnFirstFailure)
                {
                    break;
                }
            }
        }

        stopwatch.Stop();

        return new ValidationSummary
        {
            Results = results,
            TotalDuration = stopwatch.Elapsed,
            Passed = results.All(r => r.Passed),
            PassedCount = results.Count(r => r.Passed),
            FailedCount = results.Count(r => !r.Passed)
        };
    }
}

/// <summary>
/// Summary of all validation results.
/// </summary>
public class ValidationSummary
{
    /// <summary>
    /// Gets the individual validation results.
    /// </summary>
    public required IReadOnlyList<ValidationResult> Results { get; init; }

    /// <summary>
    /// Gets the total duration of all validations.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Gets whether all validators passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Gets the count of passed validators.
    /// </summary>
    public int PassedCount { get; init; }

    /// <summary>
    /// Gets the count of failed validators.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Gets the failed validation results.
    /// </summary>
    public IEnumerable<ValidationResult> FailedResults => 
        Results.Where(r => !r.Passed);
}
