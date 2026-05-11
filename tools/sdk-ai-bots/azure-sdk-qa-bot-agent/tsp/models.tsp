import "@typespec/http";
import "@typespec/versioning";

using TypeSpec.Http;
using TypeSpec.Versioning;

namespace AzureSdkQaBot;

// ── Unions ────────────────────────────────────────────────────

@doc("Message roles in the conversation")
union Role {
  string,
  
  @doc("User message")
  User: "user",

  @doc("Assistant message")
  Assistant: "assistant",

  @doc("System message")
  System: "system",

  @doc("Developer message")
  Developer: "developer",
}

@doc("Types of additional information")
union AdditionalInfoType {
  @doc("Link to external resource")
  Link: "link",

  @doc("Image content")
  Image: "image",

  @doc("Text content")
  Text: "text",
}

@doc("User reaction types for feedback")
union Reaction {
  @doc("Positive feedback")
  Good: "good",

  @doc("Negative feedback")
  Bad: "bad",

  @doc("Unknown reaction")
  Unknown: "unknown",
}

@doc("Conversation type")
union ConversationType {
  @doc("Teams channel conversation")
  TeamsChannel: "teams_channel",
}

// ── Chat Models ──────────────────────────────────────────────

@doc("A message in the conversation")
model Message {
  @doc("The role of the message sender")
  role: Role;

  @doc("The content of the message")
  content: string;

  @doc("Optional display name of the user")
  user_name?: string;

  @doc("Optional user identifier")
  user_id?: string;
}

@doc("Additional information to provide to the agent")
model AdditionalInfo {
  @doc("The type of the additional information")
  type: AdditionalInfoType;

  @doc("The content of the additional information")
  content: string;

  @doc("The link to the additional information, required if type is link")
  link?: string;
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

@doc("Request for chat completion")
model ChatRequest {
  @doc("The tenant ID")
  tenant_id: string;

  @doc("Existing conversation identifier, if continuing a conversation")
  conversation_id?: string;

  @doc("The conversation type")
  conversation_type?: ConversationType;

  @doc("The message to send to the agent")
  message: Message;

  @doc("Whether to include full context in the response. Default is false")
  with_full_context?: boolean;

  @doc("Additional information to provide to the agent, such as links or images")
  additional_infos?: AdditionalInfo[];
}

@doc("Response from chat completion")
model ChatResponse {
  @doc("The unique ID of the response")
  id: string;

  @doc("The answer from the agent")
  answer: string;

  @doc("The references to the documents used to generate the answer")
  references?: Reference[];

  @doc("The full context used to generate the answer")
  full_context?: string;

  @doc("Whether the agent produced a non-empty answer")
  has_result: boolean;

  @doc("The tenant ID the question is routed to")
  route_tenant?: string;
}

// ── Feedback Models ──────────────────────────────────────────

@doc("Request for submitting feedback")
model FeedbackRequest {
  @doc("Teams channel identifier")
  channel_id?: string;

  @doc("The tenant ID")
  tenant_id?: string;

  @doc("User's reaction to the conversation")
  reaction?: Reaction;

  @doc("Optional comment from the user about the conversation")
  comment?: string;

  @doc("Optional reasons for the feedback")
  reasons?: string[];

  @doc("Optional link to the conversation for further reference")
  link?: string;

  @doc("Optional user name")
  user_name?: string;
}

@doc("Response for feedback submission")
model FeedbackResponse {
  @doc("Whether the feedback was persisted")
  saved?: boolean;

  @doc("URL of the GitHub issue created from the feedback, if any")
  issue_url?: string;
}

// ── Conversation Models ──────────────────────────────────────

@doc("Extra metadata for a conversation message")
model ConversationMessageExtraInfo {
  @doc("Teams channel identifier")
  channel_id?: string;

  @doc("Direct link to the message")
  message_link?: string;
}

@doc("A conversation message to save")
model ConversationMessage {
  @doc("Message identifier (from Teams)")
  id: string;

  @doc("Tenant identifier")
  tenant_id?: string;

  @doc("Role of the sender")
  sender_role: Role;

  @doc("Sender's user identifier")
  sender_id: string;

  @doc("Sender's display name")
  sender_name: string;

  @doc("Message content")
  content: string;

  @doc("UTC timestamp when the message was created")
  created_at: utcDateTime;

  @doc("Customer conversation identifier")
  conversation_id?: string;

  @doc("Conversation type")
  conversation_type?: ConversationType;

  @doc("Additional message metadata")
  extra_info?: ConversationMessageExtraInfo;
}

@doc("Response returned after saving a conversation message")
model SaveConversationMessageResponse {
  // Empty response model
}

// ── Intention Models ─────────────────────────────────────────

@doc("Request for intention classification")
model IntentionRequest {
  @doc("The message to classify")
  message: Message;

  @doc("Existing conversation identifier")
  conversation_id?: string;

  @doc("The conversation type")
  conversation_type?: ConversationType;
}

@doc("Response from intention classification")
model IntentionResponse {
  @doc("Whether the bot should auto-reply to the message")
  should_respond: boolean;

  @doc("The reason for the classification decision")
  reason: string;
}

// ── Health Check ─────────────────────────────────────────────

@doc("Health check response")
model PingResponse {
  @doc("Service status")
  status: "ok";

  @doc("Service version")
  version: string;
}
