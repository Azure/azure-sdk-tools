import "@typespec/http";
import "@typespec/rest";
import "./model.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;

namespace AzureSDKQABot;

@route("/ping")
interface HealthCheck {
  @doc("Health check endpoint - no authentication required")
  @get
  ping(): {
    @statusCode statusCode: 200;
    @body body: string;
  };
}

@route("/completion")
interface Completion {
  @doc("The core Q&A API. Generate a completion response for the given message and context")
  @post
  completion(
    @header("X-API-Key") apiKey: string,
    @body request: CompletionRequest
  ): CompletionResponse | ErrorResponse | UnauthorizedError;
}

@route("/feedback")
interface Feedback {
  @doc("Submit feedback for a conversation")
  @post
  feedback(
    @header("X-API-Key") apiKey: string,
    @body request: FeedbackRequest
  ): FeedbackResponse | ErrorResponse | UnauthorizedError;
}
