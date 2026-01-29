import "@typespec/http";
import "@typespec/versioning";

using TypeSpec.Http;
using TypeSpec.Versioning;

namespace AzureSDK.QAAssistant;

@doc("Available tenant IDs for the QA bot service")
union TenantID {
  @doc("Default Azure SDK QA bot")
  AzureSDKQaBot: "azure_sdk_qa_bot",
  
  @doc("TypeSpec extension bot")
  TypeSpecExtension: "typespec_extension",
  
  @doc("Python channel QA bot")
  PythonChannelQaBot: "python_channel_qa_bot",
  
  @doc(".NET channel QA bot")
  DotnetChannelQaBot: "dotnet_channel_qa_bot",
  
  @doc("Azure SDK onboarding bot")
  AzureSDKOnboarding: "azure_sdk_onboarding",
  
  @doc("Golang channel QA bot")
  GolangChannelQaBot: "golang_channel_qa_bot",
  
  @doc("Java channel QA bot")
  JavaChannelQaBot: "java_channel_qa_bot",
  
  @doc("JavaScript channel QA bot")
  JavaScriptChannelQaBot: "javascript_channel_qa_bot",
  
  @doc("General QA bot")
  GeneralQaBot: "general_qa_bot",
  
  @doc("API spec review bot")
  APISpecReviewBot: "api_spec_review_bot",
}

@doc("Available data sources for knowledge retrieval")
union Source {
  @doc("TypeSpec documentation")
  TypeSpecDocs: "typespec_docs",
  
  @doc("TypeSpec Azure documentation")
  TypeSpecAzureDocs: "typespec_azure_docs",
  
  @doc("Azure REST API specifications wiki")
  AzureRestAPISpecsWiki: "azure_rest_api_specs_wiki",
  
  @doc("Azure SDK for Python documentation")
  AzureSDKForPythonDocs: "azure_sdk_for_python_docs",
  
  @doc("Azure SDK for Python wiki")
  AzureSDKForPythonWiki: "azure_sdk_for_python_wiki",
  
  @doc("Existed TypeSpec Q&A")
  StaticTypeSpecQA: "static_typespec_qa",
  
  @doc("Azure API guidelines")
  AzureAPIGuidelines: "azure_api_guidelines",
  
  @doc("Azure Resource Manager RPC")
  AzureResourceManagerRPC: "azure_resource_manager_rpc",
  
  @doc("TypeSpec migration documentation")
  TypeSpecMigration: "static_typespec_migration_docs",
  
  @doc("Azure SDK documentation engineering")
  AzureSDKDocsEng: "azure-sdk-docs-eng",
  
  @doc("Azure SDK internal wiki")
  AzureSDKInternalWiki: "azure-sdk-internal-wiki",
  
  @doc("Azure SDK guidelines")
  AzureSDKGuidelines: "azure-sdk-guidelines",
  
  @doc("TypeSpec Azure HTTP specs")
  TypeSpecAzureHttpSpecs: "typespec_azure_http_specs",
  
  @doc("TypeSpec HTTP specs")
  TypeSpecHttpSpecs: "typespec_http_specs",
  
  @doc("Azure SDK for Go documentation")
  AzureSDKForGoDocs: "azure_sdk_for_go_docs",
  
  @doc("Static Azure documentation")
  StaticAzureDocs: "static_azure_docs",
  
  @doc("TypeSpec to Swagger mapping")
  StaticTypeSpecToSwaggerMapping: "static_typespec_to_swagger_mapping",
  
  @doc("Azure SDK for Java documentation")
  AzureSDKForJavaDocs: "azure_sdk_for_java_docs",
  
  @doc("Azure SDK for Java wiki")
  AzureSDKForJavaWiki: "azure_sdk_for_java_wiki",
  
  @doc("Autorest Java documentation")
  AutorestJavaDocs: "autorest_java_docs",
  
  @doc("Azure SDK for JavaScript documentation")
  AzureSDKForJavaScriptDocs: "azure_sdk_for_js_docs",
  
  @doc("Azure SDK for JavaScript wiki")
  AzureSDKForJavaScriptWiki: "azure_sdk_for_js_wiki",
  
  @doc("Azure SDK for .NET documentation")
  AzureSDKForNetDocs: "azure_sdk_for_net_docs",
}

@doc("Message roles in the conversation")
union Role {
  @doc("User message")
  User: "user",
  
  @doc("Assistant message")
  Assistant: "assistant",
  
  @doc("System message")
  System: "system",
}

@doc("Types of additional information")
union AdditionalInfoType {
  @doc("Link to external resource")
  Link: "link",
  
  @doc("Image content")
  Image: "image",
}

@doc("User reaction types for feedback")
union Reaction {
  @doc("Positive feedback")
  Good: "good",
  
  @doc("Negative feedback")
  Bad: "bad",
}

@doc("Question scope for intent analysis")
union QuestionScope {
  @doc("Unknown scope")
  Unknown: "unknown",
  
  @doc("Branded question (specific to Azure/Microsoft products)")
  Branded: "branded",
  
  @doc("Unbranded question (general programming/technical)")
  Unbranded: "unbranded",
}

@doc("Service type for filtering")
union ServiceType {
  @doc("Unknown service type")
  Unknown: "unknown",
  
  @doc("Data plane service")
  DataPlane: "data-plane",
  
  @doc("Management plane service")
  ManagementPlane: "management-plane",
}

@doc("A message in the conversation")
model Message {
  @doc("The role of the message sender")
  role: Role;
  
  @doc("The content of the message")
  content: string;
  
  @doc("The raw content of the message, used for searching")
  raw_content?: string;
  
  @doc("The name of the message sender, used for system messages")
  name?: string;
}

@doc("A reference to a document used to generate the answer")
model Reference {
  @doc("The title of the document")
  title: string;
  
  @doc("The source of the document")
  source: string;
  
  @doc("The link to the document")
  link: string;
  
  @doc("The content of the document")
  content: string;
}

@doc("Additional information to provide to the agent")
model AdditionalInfo {
  @doc("The type of the additional information")
  type: AdditionalInfoType;
  
  @doc("The content of the additional information")
  content: string;
  
  @doc("The link to the additional information, required if type is link")
  link: string;
}

@doc("Intent analysis result for the question")
model Intention {
  @doc("The question to ask the agent")
  question: string;
  
  @doc("The category of the question")
  category: string;
  
  @doc("The type of the spec, such as typespec, azure rest api, etc.")
  spec_type?: string;
  
  @doc("Whether to invoke RAG workflow")
  needs_rag_processing: boolean;
  
  @doc("The scope of the question")
  question_scope?: QuestionScope;
  
  @doc("The service type for filtering")
  service_type?: ServiceType;
}

@doc("Request for chat completion")
model CompletionRequest {
  @doc("The tenant ID of the agent")
  tenant_id: TenantID;
  
  @doc("The number of top K documents to search for the answer. Default is 10")
  top_k?: int32;
  
  @doc("The sources to search for the answer. Default is all")
  sources?: Source[];
  
  @doc("The message to send to the agent")
  message: Message;

  @doc("The history of messages communicated with the agent")
  history?: Message[];
  
  @doc("Whether to use the full context for the agent. Default is false")
  with_full_context?: boolean;
  
  @doc("Whether to preprocess the message before sending it to the agent. Default is false")
  with_preprocess?: boolean;
  
  @doc("Additional information to provide to the agent, such as links or images")
  additional_infos?: AdditionalInfo[];
  
  @doc("Optional intention fields that override LLM intention recognition results")
  intention?: Intention;
}

@doc("Response from chat completion")
model CompletionResponse {
  @doc("The unique ID of the completion")
  id: string;
  
  @doc("The answer from the agent")
  answer: string;
  
  @doc("Whether the agent has a result")
  has_result: boolean;
  
  @doc("The references to the documents used to generate the answer")
  references?: Reference[];
  
  @doc("The full context used to generate the answer")
  full_context?: string;
  
  @doc("The intention analysis of the question")
  intention?: Intention;
  
  @doc("The reasoning progress of generating the answer")
  reasoning_progress?: string;
  
  @doc("The tenant ID the question is routed to")
  route_tenant?: TenantID;
}

@doc("Request for submitting feedback")
model FeedbackRequest {
  @doc("The tenant ID")
  tenant_id: string;
  
  @doc("The conversation messages")
  messages: Message[];
  
  @doc("User's reaction to the conversation")
  reaction: Reaction;
  
  @doc("Optional comment from the user about the conversation")
  comment?: string;
  
  @doc("Channel ID")
  channel_id?: string;

  @doc("Optional user name")
  user_name?: string;

  @doc("Optional reasons for the feedback, used to improve the model")
  reasons?: string[];

  @doc("Optional link to the conversation for further reference")
  link?: string;
}

@doc("Response for feedback submission")
model FeedbackResponse {
  // Empty response model
}

@doc("Simple error response used by feedback API")
@error
model SimpleErrorResponse {
  @doc("Error message")
  error: string;
}

@doc("Error codes for the service")
union ErrorCode {
  // Client Error Codes (4xx)
  @doc("Invalid request format or parameters")
  InvalidRequest: "INVALID_REQUEST",
  
  @doc("Message field is missing")
  MissingMessage: "MISSING_MESSAGE",
  
  @doc("Message content is empty")
  EmptyContent: "EMPTY_CONTENT",
  
  @doc("Invalid tenant ID provided")
  InvalidTenantID: "INVALID_TENANT_ID",
  
  @doc("Authentication failed")
  Unauthorized: "UNAUTHORIZED",
  
  // Server Error Codes (5xx)
  @doc("Service initialization failed")
  ServiceInitFailure: "SERVICE_INIT_FAILURE",
  
  @doc("LLM service call failed")
  LLMServiceFailure: "LLM_SERVICE_FAILURE",
  
  @doc("LLM rate limit exceeded")
  LLMRateLimitFailure: "LLM_RATE_LIMIT_FAILURE",
  
  @doc("Search service call failed")
  SearchFailure: "SEARCH_FAILURE",
  
  @doc("Internal server error")
  InternalError: "INTERNAL_ERROR",
}

@doc("Error category for monitoring and alerting")
union ErrorCategory {
  @doc("Validation error")
  Validation: "validation",
  
  @doc("Authentication error")
  Authentication: "authentication",
  
  @doc("Authorization error")
  Authorization: "authorization",
  
  @doc("Rate limit error")
  RateLimit: "rate limit",
  
  @doc("Service error")
  Service: "service",
  
  @doc("Dependency error")
  Dependency: "dependency",
  
  @doc("Internal error")
  Internal: "internal",
}

@doc("Structured API error response")
@error
model ErrorResponse {
  @doc("Error code identifying the type of error")
  code: ErrorCode;
  
  @doc("Human-readable error message")
  message: string;
  
  @doc("Error category for monitoring and alerting")
  category: ErrorCategory;
  
  @doc("Additional error details")
  details?: unknown;
  
  @doc("Request ID for tracing")
  request_id?: string;
  
  @doc("Timestamp when the error occurred")
  timestamp?: string;
}
