// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class EvaluationSummaryTests
{
    private static EvaluationResult Result(EvaluationOutcome outcome) => new() { Outcome = outcome };

    [Test]
    public void FromResults_CountsEachOutcomeAndTotal()
    {
        var results = new[]
        {
            Result(EvaluationOutcome.PipelineFixSuccess),
            Result(EvaluationOutcome.PipelineFixSuccess),
            Result(EvaluationOutcome.ModelJudgedSuccess),
            Result(EvaluationOutcome.ModelJudgedFailure),
            Result(EvaluationOutcome.Skipped),
            Result(EvaluationOutcome.Skipped),
            Result(EvaluationOutcome.Skipped),
            Result(EvaluationOutcome.ModelError),
        };

        var summary = EvaluationSummary.FromResults(results);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Total, Is.EqualTo(8));
            Assert.That(summary.PipelineFixSuccess, Is.EqualTo(2));
            Assert.That(summary.ModelJudgedSuccess, Is.EqualTo(1));
            Assert.That(summary.ModelJudgedFailure, Is.EqualTo(1));
            Assert.That(summary.Skipped, Is.EqualTo(3));
            Assert.That(summary.ModelError, Is.EqualTo(1));
            // successCount = deterministic + model-judged successes
            Assert.That(summary.SuccessCount, Is.EqualTo(3));
        });
    }

    [Test]
    public void FromResults_Empty_AllZero()
    {
        var summary = EvaluationSummary.FromResults([]);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Total, Is.EqualTo(0));
            Assert.That(summary.PipelineFixSuccess, Is.EqualTo(0));
            Assert.That(summary.ModelJudgedSuccess, Is.EqualTo(0));
            Assert.That(summary.ModelJudgedFailure, Is.EqualTo(0));
            Assert.That(summary.Skipped, Is.EqualTo(0));
            Assert.That(summary.ModelError, Is.EqualTo(0));
            Assert.That(summary.SuccessCount, Is.EqualTo(0));
        });
    }
}
