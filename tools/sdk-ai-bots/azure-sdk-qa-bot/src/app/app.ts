import { ActivityTypes, MemoryStorage, TurnContext } from 'botbuilder';
import * as path from 'path';
import { fileURLToPath } from 'url';

// See https://aka.ms/teams-ai-library to learn more about the Teams AI library.
import { Application, ActionPlanner, PromptManager } from '@microsoft/teams-ai';
import { RAGModel } from '../models/RAGModel.js';
import { logger } from '../logging/logger.js';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { FeedbackRequestPayload, RAGOptions, sendFeedback } from '../backend/rag.js';
import config from '../config/config.js';
import { getRagTanent } from '../config/utils.js';

// Create AI components
const model = new RAGModel();

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
  logger.info(`Received feedback action: ${action} with comment: "${feedbackComment}"`, getTurnContextLogMeta(context));

  switch (action) {
    case 'feedback-like':
      const goodFeedback: FeedbackRequestPayload = {
        tenant_id: ragTanentId,
        messages: [
          {
            role: 'user',
            content: 'test good',
          },
        ],
        reaction: 'good',
        comment: feedbackComment,
      };
      await sendFeedback(goodFeedback, ragOptions);
      await context.sendActivity('You liked my service. Thanks for your feedback!');
      break;
    case 'feedback-dislike':
      const badFeedback: FeedbackRequestPayload = {
        tenant_id: ragTanentId,
        messages: [
          {
            role: 'user',
            content: 'test bad',
          },
        ],
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
