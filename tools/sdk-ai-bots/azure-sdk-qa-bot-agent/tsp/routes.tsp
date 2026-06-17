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

@route("/message")
interface Message {
  /** Classify whether the bot should auto-reply to a message. */
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

@route("/graph")
interface Graph {
  /** Run a GraphRAG Local-Search retrieval and return references.
   * Called by the chat agent's search_knowledge_graph tool. Returns an
   * empty references list (never 5xx) when the service is disabled,
   * retrieval fails, or nothing matches, so the agent can degrade
   * gracefully.
   */
  @route("/query")
  @post
  query(@body body: GraphQueryRequest): GraphSearchResult;
}

@route("/knowledge")
interface Knowledge {
  /* retrieve knowledge for a user request. */
  @route("/retrieve")
  @post
  retrieve(@body body: KnowledgeRetrieveRequest): KnowledgeRetrieveResponse;
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
