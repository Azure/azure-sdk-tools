import { ActivityTypes, MemoryStorage, TurnContext } from 'botbuilder';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { Application, ActionPlanner, PromptManager } from '@microsoft/teams-ai';
import { RAGModel } from '../models/RAGModel.js';
import { logger } from '../logging/logger.js';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { FeedbackRequestPayload, Message, RAGOptions, sendFeedback } from '../backend/rag.js';
import { extractSelectedReasons } from '../cards/components/feedback.js';
import config from '../config/config.js';
import { ConfigFacade } from '../config/configFacade.js';
import { ConversationHandler } from '../input/ConversationHandler.js';
import { parseConversationId } from '../common/shared.js';
import { ManagedIdentityCredential, TokenCredential } from '@azure/identity';
import { getAccessTokenByManagedIdentity } from '../backend/auth.js';
import { sendActivityWithRetry } from '../activityUtils.js';

// Initialize all config managers via facade
await ConfigFacade.initialize();
const channelConfigManager = ConfigFacade.getChannelConfigManager();
const tenantConfigManager = ConfigFacade.getTenantConfigManager();

const conversationHandler = new ConversationHandler();
await conversationHandler.initialize();

let credential: TokenCredential = new ManagedIdentityCredential(config.userManagedIdentityClientID);

// Create AI components
const model = new RAGModel(conversationHandler, channelConfigManager, tenantConfigManager, credential);

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
  const { channelId } = parseConversationId(context.activity.conversation.id);
  const ragTenantId = await channelConfigManager.getRagTenant(channelId);
  const ragEndpoint = await channelConfigManager.getRagEndpoint(channelId);
  const token = await getAccessTokenByManagedIdentity(credential, config.ragScope);
  const ragOptions: RAGOptions = {
    endpoint: ragEndpoint,
    accessToken: token ? token.token : undefined
  };
  const action = context.activity.value?.action;
  const feedbackComment = context.activity.value?.feedbackComment;
  const selectedReasons = extractSelectedReasons(context.activity.value);
  const meta = getTurnContextLogMeta(context);
  logger.info(
    `Received feedback action: ${action} with comment: "${feedbackComment}" and reasons: ${JSON.stringify(
      selectedReasons
    )} from user: ${context.activity.from?.name} (ID: ${context.activity.from?.id})`,
    { meta }
  );

  const conversations = await conversationHandler.getConversationMessages(context.activity.conversation.id, meta);

  const messages: Message[] = [];
  conversations.map((msg) => {
    const question: Message = msg.prompt ? { content: msg.prompt.textWithoutMention, role: 'user' } : undefined;
    if (question) messages.push(question);
    const answer: Message =
      msg.reply ? { role: 'assistant', content: msg.reply.answer } : undefined;
    if (answer) messages.push(answer);
  });

  const parsed = parseConversationId(context.activity.conversation.id);
  const postLink = parsed.postId ? generateLink(context, parsed.postId) : undefined;

  switch (action) {
    case 'feedback-like':
      const goodFeedback: FeedbackRequestPayload = {
        channel_id: channelId,
        tenant_id: ragTenantId,
        messages,
        reaction: 'good',
        comment: feedbackComment,
        reasons: selectedReasons,
        link: postLink,
        user_name: context.activity.from?.name,
      };
      await sendFeedback(goodFeedback, ragOptions, meta);
      await sendActivityWithRetry(context, 'Your feedback is received. Thank you!');
      break;
    case 'feedback-dislike':
      const badFeedback: FeedbackRequestPayload = {
        channel_id: channelId,
        tenant_id: ragTenantId,
        messages,
        reaction: 'bad',
        comment: feedbackComment,
        reasons: selectedReasons,
        link: postLink,
        user_name: context.activity.from?.name,
      };
      await sendFeedback(badFeedback, ragOptions, meta);
      await sendActivityWithRetry(context, 'Your feedback is received. Thank you!');
      break;
    default:
      break;
  }
});

function generateLink(context: TurnContext, postId: string): string {
  const activity = context.activity;

  // Extract basic information
  const tenantId = activity.conversation?.tenantId;
  const rawConversationId = activity.conversation?.id; // Raw conversation ID
  // const replyToId = activity.replyToId; // Parent message ID if this is a reply

  // Extract clean conversation ID (thread ID)
  // Format: 19:xxx@thread.tacv2;messageid=xxx -> 19:xxx@thread.tacv2
  const conversationId = rawConversationId?.split(';')[0] || rawConversationId;

  // Extract additional information from channelData if available
  const channelData = activity.channelData;
  const teamName = channelData?.team?.name || '';
  const channelName = channelData?.channel?.name || '';
  // const createdTime = activity.timestamp;

  // Build base URL
  let shareUrl = `https://teams.microsoft.com/l/message/${conversationId}/${postId}`;

  // Build query parameters
  const params = new URLSearchParams();
  if (tenantId) params.append('tenantId', tenantId);
  // if (groupId) params.append('groupId', groupId);
  // if (replyToId) params.append('parentMessageId', replyToId);
  if (teamName) params.append('teamName', encodeURIComponent(teamName));
  if (channelName) params.append('channelName', encodeURIComponent(channelName));
  // if (createdTime) params.append('createdTime', createdTime.getTime().toString());

  // Combine complete URL
  const queryString = params.toString();
  if (queryString) {
    shareUrl += `?${queryString}`;
  }

  return shareUrl;
}

export default app;
