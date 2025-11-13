using OpenAI;
using Moq;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers
{
    internal class OpenAIMockHelper
    {
        /// <summary>
        /// Creates a mock that will return a mock ChatClient when GetChatClient is called with the expected deployment name.
        /// </summary>
        /// <param name="expectedDeploymentName">The expected deployment name</param>
        /// <returns></returns>
        public static (Mock<OpenAIClient> OpenAIClient, Mock<ChatClient> ChatClient) Create(string expectedDeploymentName)
        {
            Mock<OpenAIClient> openAIClientMock = new();
            Mock<ChatClient> chatClient = new();

            openAIClientMock
                .Setup(client => client.GetChatClient(It.Is<string>(s => s == expectedDeploymentName)))
                .Returns(chatClient.Object);

            return (openAIClientMock, chatClient);
        }
    }
}
