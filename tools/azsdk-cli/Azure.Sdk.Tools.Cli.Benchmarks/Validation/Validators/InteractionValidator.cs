// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators
{
    public class InteractionValidator: IValidator
    {
        public string Name { get; }
        public InteractionValidator(string name)
        {
            Name = name;
        }
        public Task<ValidationResult> ValidateAsync(
        ValidationContext context,
        CancellationToken cancellationToken = default)
        {
            var questionAndAnswers = context.InputQuestionAndAnswers;
            if (questionAndAnswers == null || questionAndAnswers.Count == 0)
            {
                return Task.FromResult(ValidationResult.Fail(Name, "No question and answer data available."));
            }

            return Task.FromResult(ValidationResult.Pass(Name, $"Interaction between customer and agent were performed. Question and Answer: {string.Join(", ", questionAndAnswers.Select(qa => $"[{qa.Question}: {qa.Answer}]"))}"));
        }
    }
}
