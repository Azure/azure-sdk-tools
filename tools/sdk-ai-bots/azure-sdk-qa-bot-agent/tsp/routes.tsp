using TypeSpec.Http;

namespace AzureSdkQaBot;

@route("/ping")
interface Health {
  /** Health check endpoint used by App Service and the deploy pipeline. */
  @get ping(): PingResponse;
}

@route("/agent")
interface Agent {
  /** Process a chat request through the chat agent. */
  @route("/chat")
  @post
  chat(@body body: ChatRequest): ChatResponse;

  /** Process user feedback through the feedback workflow. */
  @route("/feedback")
  @post
  feedback(@body body: FeedbackRequest): FeedbackResponse;
}

@route("/conversation")
interface Conversation {
  /** Save a conversation message. */
  @route("/save")
  @post
  save(@body body: ConversationMessage): SaveConversationMessageResponse;
}

/** Legacy endpoints kept for backward compatibility. */
@route("/")
interface Legacy {
  /** Legacy chat endpoint (use /agent/chat instead). */
  @route("completion")
  @post
  completion(@body body: ChatRequest): ChatResponse;

  /** Legacy feedback endpoint (use /agent/feedback instead). */
  @route("feedback")
  @post
  feedbackLegacy(@body body: FeedbackRequest): FeedbackResponse;
}
