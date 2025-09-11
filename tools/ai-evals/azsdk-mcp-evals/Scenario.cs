using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using ModelContextProtocol.Client;

namespace azsdk_mcp_evals
{
    public class Scenario
    {
        private ChatCompletion _chatCompletion;
        public Scenario(ChatCompletion chatCompletion) 
        { 
            _chatCompletion = chatCompletion;
        }

        public async Task PlayAsync(string jsonPath)
        {
            // 1. Validate ChatHistory. ex.. Should end with AI answering and not the user
            // Before it gets here will need to be converted from JSON to chat message somehow. 
            var json = await LoadScenarioFromJsonAsync(jsonPath);
            var fullChat = json.ChatHistory.Append(json.NextMessage);

            // 2. LLM question and answer
            var response = await _chatCompletion.GetChatResponseAsync(fullChat);

            // 3. Custom Evaluator to check tool inputs
            var expectedToolInputEvaluator = new ExpectedToolInputEvaluator();

            // Pass the expected outcome through the additional context. 
            var additionalContext = new ExpectedToolInputEvaluatorContext(json.ExpectedOutcome);
            var result = await expectedToolInputEvaluator.EvaluateAsync(fullChat, response, additionalContext: [additionalContext]);
        }

        private async Task<ScenarioData> LoadScenarioFromJsonAsync(string jsonPath)
        {
            var jsonContent = await File.ReadAllTextAsync(jsonPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var scenarioData = JsonSerializer.Deserialize<ScenarioData>(jsonContent, options);

            if (scenarioData == null)
                throw new InvalidOperationException($"Failed to deserialize scenario data from {jsonPath}");

            return scenarioData;
        }
    }
}
