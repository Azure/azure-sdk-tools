// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    public class APIViewFeedbackInputTests
    {
        [Test]
        public async Task PreprocessAsync_WithRealAPIViewData()
        {
            // Arrange - Setup DI with real OpenAI but mocked APIView
            var services = new ServiceCollection();
            ServiceRegistrations.RegisterCommonServices(services, OutputHelper.OutputModes.Plain);
            
            // Mock the APIView services with sample data
            var mockApiViewService = new Mock<IAPIViewService>();
            var mockApiViewHttpService = new Mock<IAPIViewHttpService>();
            
            // Create sample comments as JSON string (matching APIView API format)
            var sampleCommentsJson = "[" +
                "{\"lineNo\":124,\"createdOn\":\"2026-01-20T22:54:07.4145628+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"Redundant - This name shouldn't end with \\\"Model\\\".\",\"isResolved\":false,\"severity\":\"Suggestion\",\"threadId\":\"nId-162291698116528618-0-1768949625810\"}," +
                "{\"lineNo\":79,\"createdOn\":\"2026-01-20T22:56:45.3208216+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"Remove \\\"widget_\\\". It's redundant.\",\"isResolved\":false,\"severity\":\"ShouldFix\",\"threadId\":\"nId-7957418941220968997-17035769901482235593-0-1768949790962\"}," +
                "{\"lineNo\":111,\"createdOn\":\"2026-01-20T22:57:16.9703341+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"Are these supposed to be public?\",\"isResolved\":false,\"severity\":\"ShouldFix\",\"threadId\":\"nId-1987662865-17035769901482235593-0-1768949825657\"}," +
                "{\"lineNo\":119,\"createdOn\":\"2026-01-20T22:57:47.4955965+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"Does this have any specific args to be listed?\",\"isResolved\":false,\"severity\":\"ShouldFix\",\"threadId\":\"nId-70433164-17035769901482235593-0-1768949849053\"}," +
                "{\"lineNo\":119,\"createdOn\":\"2026-01-23T00:46:06.0741554+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"Not that I know of.\",\"isResolved\":false,\"severity\":\"ShouldFix\",\"threadId\":\"nId-70433164-17035769901482235593-0-1768949849053\"}," +
                "{\"lineNo\":119,\"createdOn\":\"2026-01-23T00:46:12.6254839+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"This should take the required param \\\"hat\\\", which is a type string.\",\"isResolved\":false,\"severity\":\"\",\"threadId\":\"nId-70433164-17035769901482235593-0-1768949849053\"}," +
                "{\"lineNo\":111,\"createdOn\":\"2026-01-23T21:23:52.9073929+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"They are in the TypeSpec, in the 2025-11-preview version, so yes, they are supposed to be public. Or are you suggesting there should be some other hand-written method that calls this method (and make this method internal?)\",\"isResolved\":false,\"severity\":\"ShouldFix\",\"threadId\":\"nId-1987662865-17035769901482235593-0-1768949825657\"}," +
                "{\"lineNo\":111,\"createdOn\":\"2026-01-23T21:24:09.1460887+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"This will go away.\",\"isResolved\":false,\"severity\":\"ShouldFix\",\"threadId\":\"nId-1987662865-17035769901482235593-0-1768949825657\"}," +
                "{\"lineNo\":111,\"createdOn\":\"2026-01-23T21:24:22.7530074+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"All container CRUD operations were removed from TypeSpec, since those will go to the control plane APIs. Resolving.\",\"isResolved\":false,\"severity\":\"ShouldFix\",\"threadId\":\"nId-1987662865-17035769901482235593-0-1768949825657\"}," +
                "{\"lineNo\":141,\"createdOn\":\"2026-01-23T21:27:14.9926592+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"The operation status enum uses \\\"cancelled\\\" (which is the approved REST API spelling) - we should try and make this consistent.\",\"isResolved\":false,\"severity\":\"ShouldFix\",\"threadId\":\"nId-2071393708572798154131497271-0-1769203626247\"}," +
                "{\"lineNo\":141,\"createdOn\":\"2026-01-23T21:27:26.6066359+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"TODO: Review all other instances of canceled/cancelled. Change in TypeSpec.\",\"isResolved\":false,\"severity\":\"\",\"threadId\":\"nId-2071393708572798154131497271-0-1769203626247\"}," +
                "{\"lineNo\":141,\"createdOn\":\"2026-01-23T21:27:48.7620168+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"It's a mess! Contoso.WidgetManager TypeSpec includes both spelling... and since the item mentioned here needs to be aligned with the equivalent one, I don't think we have a choice but to keep it as is.\",\"isResolved\":false,\"severity\":\"\",\"threadId\":\"nId-2071393708572798154131497271-0-1769203626247\"}," +
                "{\"lineNo\":101,\"createdOn\":\"2026-01-23T21:29:24.3328862+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"We should return None on a delete operation.\",\"isResolved\":false,\"severity\":\"ShouldFix\",\"threadId\":\"nId-81016050799401034-1120274748-784388137-0-1769203767351\"}," +
                "{\"lineNo\":101,\"createdOn\":\"2026-01-23T21:29:29.8802309+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"This applies to all the delete operations.\",\"isResolved\":false,\"severity\":\"\",\"threadId\":\"nId-81016050799401034-1120274748-784388137-0-1769203767351\"}," +
                "{\"lineNo\":101,\"createdOn\":\"2026-01-23T21:29:39.5807383+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"There REST API returns an object with some information. Hard for me to say if that's useful. Should we not do the same in the emitted Python code? Is there a way in TypeSpec to tell the Python emitter to ignore a returned object?\",\"isResolved\":false,\"severity\":\"\",\"threadId\":\"nId-81016050799401034-1120274748-784388137-0-1769203767351\"}," +
                "{\"lineNo\":101,\"createdOn\":\"2026-01-23T21:29:51.2421148+00:00\",\"upvotes\":0,\"downvotes\":0,\"createdBy\":\"swathipil\",\"commentText\":\"TODO: return None instead. Try doing it via TypeSpec. (decorator alttype, revtype?)\",\"isResolved\":false,\"severity\":\"\",\"threadId\":\"nId-81016050799401034-1120274748-784388137-0-1769203767351\"}" +
                "]";
            
            var sampleMetadataJson = "{\"packageName\":\"azure-contoso-widgetmanager\",\"language\":\"Python\",\"revisionLabel\":\"Test Revision\"}";
            
            mockApiViewService
                .Setup(s => s.GetCommentsByRevisionAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(sampleCommentsJson);
            
            mockApiViewHttpService
                .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(sampleMetadataJson);
            
            // Replace the services with mocks
            services.AddSingleton(mockApiViewService.Object);
            services.AddSingleton(mockApiViewHttpService.Object);
            
            var provider = services.BuildServiceProvider();
            var helper = provider.GetRequiredService<IAPIViewFeedbackCustomizationsHelpers>();
            var logger = provider.GetRequiredService<ILogger<APIViewFeedbackInput>>();

            // Create the input processor
            var feedbackInput = new APIViewFeedbackInput(
                "https://apiview.dev/review/test-review-id?activeApiRevisionId=test-revision-id",
                helper,
                logger);

            // Act - Process the feedback which uses REAL OpenAI for consolidation
            var result = await feedbackInput.PreprocessAsync();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FeedbackItems, Is.Not.Null);
            Assert.That(result.FeedbackItems.Count, Is.EqualTo(6), "Should have 6 consolidated comment threads");
            Assert.That(result.Language, Is.EqualTo("Python"));
            Assert.That(result.PackageName, Is.EqualTo("azure-contoso-widgetmanager"));
            Assert.That(result.InputType, Is.EqualTo("apiview"));
            
            // Output all results from OpenAI to console
            Console.WriteLine($"\n=== Found {result.FeedbackItems.Count} consolidated feedback items ===\n");
            foreach (var item in result.FeedbackItems)
            {
                Console.WriteLine($"ID: {item.Id}");
                Console.WriteLine($"Comment: {item.Comment}\n");
            }
        }
    }
}
