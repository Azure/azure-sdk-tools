// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IssueLabeler.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Hubbup.MikLabelModel.Tests
{
    public class LabelerLiteTests
    {
        Mock<IConfiguration> mockConfig = new Mock<IConfiguration>();
        Mock<ILogger<LabelerLite>> mockLogger = new Mock<ILogger<LabelerLite>>();
        Mock<IModelHolderFactoryLite> mockModelFactory = new Mock<IModelHolderFactoryLite>();
        Mock<IPredictor> mockPredictor = new Mock<IPredictor>();
        ILabelerLite? target;

        private const string owner = "someOwner";
        private const string repo = "someRepo";
        private const int number = 1234;
        private const int lowConfidenceNumber = 5678;
        private LabelSuggestion suggestion = new LabelSuggestion { LabelScores = new List<ScoredLabel> { new ScoredLabel { LabelName = "someLabel", Score = 0.99f } } };
        private LabelSuggestion lowConfidenceSuggestion = new LabelSuggestion { LabelScores = new List<ScoredLabel> { new ScoredLabel { LabelName = "someLabel", Score = 0.20f } } };

        [SetUp]
        public void Setup()
        {
            mockModelFactory.Setup(m => m.GetPredictor(owner, repo, It.IsAny<String>())).ReturnsAsync(mockPredictor.Object);
            mockPredictor.Setup(m => m.Predict(It.Is<GitHubIssue>(i => i.ID != lowConfidenceNumber))).ReturnsAsync(suggestion);
            mockPredictor.Setup(m => m.Predict(It.Is<GitHubIssue>(i => i.ID == lowConfidenceNumber))).ReturnsAsync(lowConfidenceSuggestion);

            target = new LabelerLite(mockLogger.Object, mockModelFactory.Object, mockConfig.Object);
        }

        [Test]
        public async Task QueryPredictionsReturnsSuggestionsWhenPresent()
        {
            var predictions = await target.QueryLabelPrediction(number, "This is some title", "This is some body", "someone", repo, owner);

            Assert.That(predictions, Is.Not.Null);
            Assert.That(predictions.First(), Is.EqualTo(suggestion.LabelScores.Single().LabelName));
        }

        [Test]
        public async Task QueryPredictionsReturnsEmptySetWhenNoPredictionsPresent()
        {
            var predictions = await target.QueryLabelPrediction(lowConfidenceNumber, "This is some title", "This is some body", "someone", repo, owner);

            Assert.That(predictions, Is.Not.Null);
            Assert.That(predictions.Count, Is.EqualTo(0));
        }
    }
}