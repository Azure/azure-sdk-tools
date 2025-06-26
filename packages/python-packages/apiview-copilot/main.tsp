// main.tsp - TypeSpec API surface for apiview-copilot FastAPI app

using TypeSpec.Rest;
using TypeSpec.OpenAPI3;

@service({
  title: "APIView Copilot Service",
  version: "1.0.0"
})
@useDependency(RestResource)
@useDependency(OpenAPI3Resource)

namespace ApiViewCopilot {

  @route("/{language}")
  @post
  op legacyApiReview(@path language: string, @body request: ApiReviewRequest): ApiReviewResponse;

  @route("/api-review/start")
  @post
  op startApiReviewJob(@body request: ApiReviewJobRequest): ApiReviewJobStartResponse;

  @route("/api-review/{job_id}")
  @get
  op getApiReviewJobStatus(@path job_id: string): ApiReviewJobStatusResponse;

  @route("/api-review/summarize")
  @post
  op summarizeApi(@body request: SummarizeRequest): SummarizeResponse;

  @route("/agent/chat")
  @post
  op agentChat(@body request: AgentChatRequest): AgentChatResponse;

  model ApiReviewRequest {
    target: string;
    base?: string;
    outline?: string;
    comments?: any[];
  }

  model ApiReviewResponse {
    // The full review result, structure depends on implementation
    [key: string]: any;
  }

  model ApiReviewJobRequest {
    language: string;
    target: string;
    base?: string;
    outline?: string;
    comments?: any[];
    target_id?: string;
  }

  model ApiReviewJobStartResponse {
    job_id: string;
  }

  model ApiReviewJobStatusResponse {
    status: "InProgress" | "Success" | "Error";
    comments?: any[];
    details?: string;
  }

  model SummarizeRequest {
    language: string;
    target: string;
    base?: string;
  }

  model SummarizeResponse {
    summary: string;
  }

  model AgentChatRequest {
    user_input: string;
    thread_id?: string;
    messages?: any[];
  }

  model AgentChatResponse {
    response: string;
    thread_id: string;
    messages: any[];
  }
}
