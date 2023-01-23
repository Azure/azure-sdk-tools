using System.IO;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests
{
    public static class TestHelpers
    {
        public static string GetTestEventPayload(string eventJsonFile)
        {
            string rawJson = File.ReadAllText(eventJsonFile);
            return rawJson;
        }
    }
}
