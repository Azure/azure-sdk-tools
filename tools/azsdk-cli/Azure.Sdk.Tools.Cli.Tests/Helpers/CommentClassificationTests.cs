// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenAI;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    /// <summary>
    /// Tests for comment classification template and classifier
    /// </summary>
    /// <remarks>
    /// TODO: These tests validate classification logic (PHASE_A/SUCCESS/FAILURE decisions and output format)
    /// but do NOT validate that the fetch_documentation tool is actually being called. Tests bypass the
    /// microagent orchestration by calling ChatClient directly, so the LLM has no access to tools.
    /// Consider implementing integration tests that invoke through CustomizationFromFeedbackTool.ClassifyAsync
    /// with full DI and tools to validate end-to-end behavior including documentation fetching.
    /// </remarks>
    public class CommentClassificationTests
    {
        private ServiceProvider? _serviceProvider;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();
            ServiceRegistrations.RegisterCommonServices(services, OutputHelper.OutputModes.Plain);
            _serviceProvider = services.BuildServiceProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task ClassifyPhaseB_CustomizationDriftAfterRename_ReturnsFailure()
        {
            // Arrange - Build error after Phase B customization updates
            var request = @"--- Iteration 1 ---
Rename displayName -> name in TypeSpec for DocumentIntelligence service

--- TypeSpec Changes Applied ---
Added @@clientName(DocumentIntelligence.displayName, ""name"") to client.tsp

--- Build Result ---
SDK regenerated successfully. Generated model now has 'name' instead of 'displayName'.
Build failed due to customization drift.

--- Code Changes Applied ---
Updated customization files to reference 'name' instead of 'displayName':
- Java: Updated references in /customization/*Customization.java files

--- Build Result ---
Build failed with error:
cannot find symbol: method getField(String)
Note: Field 'displayName' no longer exists in generated model

--- Iteration 2 ---
Build error after Phase B customization updates

--- NextSteps ---
Issue: Build still failing after patches applied
BuildError: cannot find symbol: method getField(String)
Note: Field 'displayName' no longer exists in generated model
SuggestedApproach: The customization code may have additional references to 'displayName' that were not caught by automated patching. Review all customization files for:
1. String literals containing ""displayName"" (e.g., in getField() calls)
2. Method names containing displayName
3. Variable names or constants referencing displayName
4. Comments or documentation strings
Documentation: https://raw.githubusercontent.com/Azure/autorest.java/refs/heads/main/customization-base/README.md";

            var template = new CommentClassificationTemplate(
                serviceName: "DocumentIntelligence",
                language: "java",
                request: request,
                iteration: 2,
                isStalled: false
            );

            var prompt = template.BuildPrompt();

            // Get OpenAI client from DI
            var openAIClient = _serviceProvider!.GetRequiredService<OpenAIClient>();
            var chatClient = openAIClient.GetChatClient("gpt-4o");

            // Act - Call the classifier
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt)
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var classification = response.Value.Content[0].Text;

            // Assert
            Assert.That(classification, Is.Not.Null);
            Assert.That(classification, Does.Contain("Classification: FAILURE"));
            Assert.That(classification, Does.Contain("Iteration: 2"));
            Assert.That(classification, Does.Contain("Phase B exhausted").Or.Contains("patches applied but build still failing"));

            // Output for debugging
            Console.WriteLine("=== Classification Result ===");
            Console.WriteLine(classification);
        }

        [Test]
        public async Task ClassifyPhaseA_SimpleRename_ReturnsPhaseA()
        {
            // Arrange - Simple rename request
            var request = "Rename FooClient to BarClient for .NET";

            var template = new CommentClassificationTemplate(
                serviceName: "Widget",
                language: "csharp",
                request: request,
                iteration: 1,
                isStalled: false
            );

            var prompt = template.BuildPrompt();

            // Get OpenAI client from DI
            var openAIClient = _serviceProvider!.GetRequiredService<OpenAIClient>();
            var chatClient = openAIClient.GetChatClient("gpt-4o");

            // Act
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt)
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var classification = response.Value.Content[0].Text;

            // Assert
            Assert.That(classification, Is.Not.Null);
            Assert.That(classification, Does.Contain("Classification: PHASE_A"));
            Assert.That(classification, Does.Contain("Iteration: 1"));
            Assert.That(classification, Does.Contain("@@clientName").Or.Contains("clientName"));

            Console.WriteLine("=== Classification Result ===");
            Console.WriteLine(classification);
        }

        [Test]
        public async Task ClassifySuccess_BuildPassed_ReturnsSuccess()
        {
            // Arrange - Build success after changes
            var request = @"Rename FooClient to BarClient for .NET
--- TypeSpec Changes Applied ---
Added @@clientName(FooClient, ""BarClient"", ""csharp"")
--- Build Result ---
Build succeeded.";

            var template = new CommentClassificationTemplate(
                serviceName: "Widget",
                language: "csharp",
                request: request,
                iteration: 2,
                isStalled: false
            );

            var prompt = template.BuildPrompt();

            // Get OpenAI client from DI
            var openAIClient = _serviceProvider!.GetRequiredService<OpenAIClient>();
            var chatClient = openAIClient.GetChatClient("gpt-4o");

            // Act
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt)
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var classification = response.Value.Content[0].Text;

            // Assert
            Assert.That(classification, Is.Not.Null);
            Assert.That(classification, Does.Contain("Classification: SUCCESS"));
            Assert.That(classification, Does.Contain("Iteration: 2"));

            Console.WriteLine("=== Classification Result ===");
            Console.WriteLine(classification);
        }

        [Test]
        public async Task ClassifyFailure_Stalled_ReturnsFailure()
        {
            // Arrange - Stalled on same error
            var request = @"Fix import error
--- Iteration 2 ---
--- Build Result ---
error CS0246: The type 'FooModel' could not be found
--- Iteration 3 ---
--- Code Changes Applied ---
Added using statement for FooModel
--- Build Result ---
error CS0246: The type 'FooModel' could not be found";

            var template = new CommentClassificationTemplate(
                serviceName: "Widget",
                language: "csharp",
                request: request,
                iteration: 3,
                isStalled: true
            );

            var prompt = template.BuildPrompt();

            // Get OpenAI client from DI
            var openAIClient = _serviceProvider!.GetRequiredService<OpenAIClient>();
            var chatClient = openAIClient.GetChatClient("gpt-4o");

            // Act
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt)
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var classification = response.Value.Content[0].Text;

            // Assert
            Assert.That(classification, Is.Not.Null);
            Assert.That(classification, Does.Contain("Classification: FAILURE"));
            Assert.That(classification, Does.Contain("Stalled").Or.Contains("same error"));

            Console.WriteLine("=== Classification Result ===");
            Console.WriteLine(classification);
        }

        [Test]
        public async Task ClassifySuccess_KeepAsIs_ReturnsSuccess()
        {
            // Arrange - Non-actionable "keep as is" comment
            var request = "Keep this as is since cancelled/canceled are both used in Contoso.WidgetManager TypeSpec.";

            var template = new CommentClassificationTemplate(
                serviceName: "Widget",
                language: "csharp",
                request: request,
                iteration: 1,
                isStalled: false
            );

            var prompt = template.BuildPrompt();

            // Get OpenAI client from DI
            var openAIClient = _serviceProvider!.GetRequiredService<OpenAIClient>();
            var chatClient = openAIClient.GetChatClient("gpt-4o");

            // Act
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt)
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var classification = response.Value.Content[0].Text;

            // Assert
            Assert.That(classification, Is.Not.Null);
            Assert.That(classification, Does.Contain("Classification: SUCCESS"));
            Assert.That(classification, Does.Contain("keep").Or.Contains("no action"));

            Console.WriteLine("=== Classification Result ===");
            Console.WriteLine(classification);
        }
    }
}
