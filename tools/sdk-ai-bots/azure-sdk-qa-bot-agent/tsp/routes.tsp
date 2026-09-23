using TypeSpec.Http;

namespace AzureSdkQaBot;

@route("/ping")
interface Health {
  /** Health check endpoint used by App Service and the deploy pipeline. */
  @get ping(): PingResponse;
}

@route("/agent")
interface Agent {
  /** Generate an answer, apply reply policy, and reserve eligible expert-notification attempts. */
  @route("/chat")
  @post
  chat(@body body: ChatRequest): ChatResponse;

  /** Process user feedback through the feedback workflow. */
  @route("/feedback")
  @post
  feedback(@body body: FeedbackRequest): FeedbackResponse;
}

/** Channel routing and participation policy lookup. */
@route("/config")
interface Config {
  /** Resolve channel settings with backward-compatible defaults. */
  @route("/channel")
  @get
  channel(@query channel_id: string): ChannelConfigResponse;
}

@route("/message")
interface MessageOperations {
  /** Classify whether the bot should auto-reply to a message. */
  @TypeSpec.OpenAPI.operationId("Message_intention")
  @route("/intention")
  @post
  intention(@body body: IntentionRequest): IntentionResponse;
}

@route("/conversation")
interface Conversation {
  /** Save a conversation message. */
  @route("/save")
  @post
  save(@body body: ConversationMessage): SaveConversationMessageResponse;
}

@route("/knowledge")
interface Knowledge {
  /* retrieve knowledge for a user request. */
  @route("/retrieve")
  @post
  retrieve(@body body: KnowledgeRetrieveRequest): KnowledgeRetrieveResponse
}

/** Legacy endpoints kept for backward compatibility. */
@route("/")
interface Legacy {
  /** Alias of /agent/chat, including confidence and participation decisions. */
  @route("completion")
  @post
  completion(@body body: ChatRequest): ChatResponse;

  /** Legacy feedback endpoint (use /agent/feedback instead). */
  @route("feedback")
  @post
  feedbackLegacy(@body body: FeedbackRequest): FeedbackResponse;
}
