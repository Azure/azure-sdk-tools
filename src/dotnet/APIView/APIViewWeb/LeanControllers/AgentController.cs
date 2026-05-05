using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Models;
using APIViewWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.LeanControllers
{
    public class AgentController : BaseApiController
    {
        private readonly ILogger<AgentController> _logger;
        private readonly string _copilotEndpoint;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICopilotAuthenticationService _copilotAuthService;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public AgentController(
            ILogger<AgentController> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ICopilotAuthenticationService copilotAuthService)
        {
            _logger = logger;
            _copilotEndpoint = configuration["CopilotServiceEndpoint"];
            _httpClientFactory = httpClientFactory;
            _copilotAuthService = copilotAuthService;
        }

        /// <summary>
        /// Report an issue from the APIView UI. Forwards to the apiview-copilot
        /// service which uses an LLM to draft a GitHub issue title/body and
        /// creates the issue.
        /// </summary>
        [HttpPost("report-issue", Name = "ReportIssue")]
        public async Task<ActionResult<ReportIssueResponse>> ReportIssueAsync([FromBody] ReportIssueRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest("description is required");
            }

            if (string.IsNullOrEmpty(_copilotEndpoint))
            {
                _logger.LogError("CopilotServiceEndpoint is not configured.");
                return StatusCode(StatusCodes.Status502BadGateway, "Issue reporting is not available.");
            }

            string targetUrl = $"{_copilotEndpoint}/report-issue";
            HttpClient client = _httpClientFactory.CreateClient();

            using HttpRequestMessage outbound = new(HttpMethod.Post, targetUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request, _jsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };
            outbound.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", await _copilotAuthService.GetAccessTokenAsync());

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(outbound);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to reach copilot service at {Url}", targetUrl);
                return StatusCode(StatusCodes.Status502BadGateway, "Failed to reach issue reporting service.");
            }

            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Copilot report-issue returned {Status}: {Body}",
                    (int)response.StatusCode,
                    responseBody);
                return StatusCode(StatusCodes.Status502BadGateway, "Failed to file issue.");
            }

            try
            {
                ReportIssueResponse parsed = JsonSerializer.Deserialize<ReportIssueResponse>(
                    responseBody, _jsonOptions);
                return new LeanJsonResult(parsed, StatusCodes.Status200OK);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse copilot report-issue response: {Body}", responseBody);
                return StatusCode(StatusCodes.Status502BadGateway, "Invalid response from issue reporting service.");
            }
        }
    }
}
