import "@typespec/http";
import "@typespec/versioning";

using TypeSpec.Http;
using TypeSpec.Versioning;

namespace AzureSDK.QAAssistant;

@doc("Available tenant IDs for the QA bot service")
union TenantID {
  @doc("Default TypeSpec QA bot")
  TypespecQABot: "typespec_qa_bot",
  
  @doc("TypeSpec extension bot")
  TypeSpecExtension: "typespec_extension",
  
  @doc("Python channel QA bot")
  PythonChannelQaBot: "python_channel_qa_bot",
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

@doc("Question categorization for intent analysis")
union QuestionCategory {
  @doc("Unknown category")
  Unknown: "unknown",
  
  @doc("Branded question (specific to Azure/Microsoft products)")
  Branded: "branded",
  
  @doc("Unbranded question (general programming/technical)")
  Unbranded: "unbranded",
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
model IntensionResult {
  @doc("The question to ask the agent")
  question: string;
  
  @doc("The category of the question")
  category: QuestionCategory;
}

@doc("Request for chat completion")
model CompletionRequest {
  @doc("The tenant ID of the agent")
  tenant_id: TenantID;
  
  @doc("The prompt template to use for the agent")
  prompt_template?: string;
  
  @doc("The arguments to use for the prompt template")
  prompt_template_arguments?: string;
  
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
  intension?: IntensionResult;
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
}

@doc("Response for feedback submission")
model FeedbackResponse {
  // Empty response model
}

@doc("Error response")
@error
model ErrorResponse {
  @doc("Error message")
  error: string;
}

@doc("Unauthorized error response")
@error
model UnauthorizedError {
  @doc("Error message indicating authentication failure")
  error: "Unauthorized - API key is missing or invalid";
}

@doc("API Key authentication scheme")
model ApiKeyAuth {
  @doc("API key for authentication")
  @header("X-API-Key")
  apiKey: string;
}
