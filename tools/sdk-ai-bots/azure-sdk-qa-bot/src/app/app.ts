import { ActivityTypes, MemoryStorage, TurnContext } from 'botbuilder';
import * as path from 'path';
import { fileURLToPath } from 'url';

// See https://aka.ms/teams-ai-library to learn more about the Teams AI library.
import { Application, ActionPlanner, PromptManager } from '@microsoft/teams-ai';
import { RAGModel } from '../models/RAGModel.js';
import { logger } from '../logging/logger.js';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { FeedbackRequestPayload, Message, RAGOptions, sendFeedback } from '../backend/rag.js';
import config from '../config/config.js';
import { getRagTanent } from '../config/utils.js';
import { ConversationHandler } from '../input/ConversationHandler.js';

const conversationHandler = new ConversationHandler();
await conversationHandler.initialize();

// Create AI components
const model = new RAGModel(conversationHandler);

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const prompts = new PromptManager({
  promptsFolder: path.join(__dirname, '../prompts'),
});

const planner = new ActionPlanner({
  model,
  prompts,
  defaultPrompt: 'chat',
});

// Define storage and application
const storage = new MemoryStorage();
const app = new Application({
  storage,
  ai: {
    planner,
    enable_feedback_loop: true,
  },
});

app.feedbackLoop(async (context, state, feedbackLoopData) => {
  //add custom feedback process logic here
  logger.info('Your feedback is ' + JSON.stringify(context.activity.value), getTurnContextLogMeta(context));
});

const isSubmitMessage = async (ctx: TurnContext) =>
  ctx.activity.type === ActivityTypes.Message && !!ctx.activity.value?.action;

app.activity(isSubmitMessage, async (context: TurnContext) => {
  const channelId = context.activity.channelId;
  const ragTanentId = getRagTanent(channelId);
  const ragOptions: RAGOptions = {
    endpoint: config.ragEndpoint,
    apiKey: config.ragApiKey,
  };
  const action = context.activity.value?.action;
  const feedbackComment = context.activity.value?.feedbackComment;
  const logMeta = getTurnContextLogMeta(context);
  logger.info(`Received feedback action: ${action} with comment: "${feedbackComment}"`, { meta: logMeta });

  const conversations = await conversationHandler.getConversationMessages(context.activity.conversation.id, logMeta);
  const messages: Message[] = [];
  conversations.map((msg) => {
    const question: Message = msg.prompt ? { content: msg.prompt.textWithoutMention, role: 'user' } : undefined;
    if (question) messages.push(question);
    const answer: Message =
      msg.reply && msg.reply.has_result ? { role: 'assistant', content: msg.reply.answer } : undefined;
    if (answer) messages.push(answer);
  });

  switch (action) {
    case 'feedback-like':
      const goodFeedback: FeedbackRequestPayload = {
        tenant_id: ragTanentId,
        messages,
        reaction: 'good',
        comment: feedbackComment,
      };
      await sendFeedback(goodFeedback, ragOptions);
      await context.sendActivity('You liked my service. Thanks for your feedback!');
      break;
    case 'feedback-dislike':
      const badFeedback: FeedbackRequestPayload = {
        tenant_id: ragTanentId,
        messages,
        reaction: 'bad',
        comment: feedbackComment,
      };
      await sendFeedback(badFeedback, ragOptions);
      await context.sendActivity('You disliked my service. Thanks for your feedback!');
      break;
    default:
      break;
  }
});
export default app;
