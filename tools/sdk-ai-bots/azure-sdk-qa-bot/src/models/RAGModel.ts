import { Memory, PromptCompletionModel, PromptFunctions, PromptTemplate, Tokenizer } from '@microsoft/teams-ai';
import { TurnContext } from 'botbuilder';
import { AdditionalInfo, CompletionRequestPayload, getRAGReply, Message, RAGOptions } from '../backend/rag.js';
import { PromptResponse } from '@microsoft/teams-ai/lib/types/PromptResponse.js';
import { ThinkingHandler } from '../turn/ThinkingHandler.js';
import config from '../config/config.js';
import { MessageWithRemoteContent, PromptGenerator } from '../input/PromptGenerator.js';
import { logger } from '../logging/logger.js';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { ChannelConfigManager } from '../config/channel.js';
import { ConversationHandler, ConversationMessage, Prompt } from '../input/ConversationHandler.js';
import { parseConversationId } from '../common/shared.js';
import { AccessToken, ManagedIdentityCredential, TokenCredential } from '@azure/identity';
import { getAccessTokenByManagedIdentity } from '../backend/auth.js';

export class RAGModel implements PromptCompletionModel {
  private readonly conversationHandler: ConversationHandler;
  private readonly promptGenerator: PromptGenerator;
  private readonly channelConfigManager: ChannelConfigManager;
  private readonly credential: TokenCredential;

  constructor(conversationHandler: ConversationHandler, channelConfigManager: ChannelConfigManager, credential: TokenCredential) {
    this.conversationHandler = conversationHandler;
    this.promptGenerator = new PromptGenerator();
    this.channelConfigManager = channelConfigManager;
    this.credential = credential;
  }


  public async completePrompt(
    context: TurnContext,
    memory: Memory,
    functions: PromptFunctions,
    tokenizer: Tokenizer,
    template: PromptTemplate
  ): Promise<PromptResponse<string>> {
    const token = await getAccessTokenByManagedIdentity(this.credential, config.ragScope);
    const meta = getTurnContextLogMeta(context);
    const { channelId } = parseConversationId(context.activity.conversation.id);
    const [ ragTenantId, ragEndpoint ] = await Promise.all([
      this.channelConfigManager.getRagTenant(channelId),
      this.channelConfigManager.getRagEndpoint(channelId),
    ]);
    logger.info(`Processing request for channel ${channelId} on rag tenant: ${ragTenantId}, endpoint: ${ragEndpoint}`, { meta });
    const ragOptions: RAGOptions = {
      endpoint: config.isLocal ? config.localBackendEndpoint : ragEndpoint,
      accessToken: token ? token.token : undefined
    };
    logger.info(`Received activity: ${JSON.stringify(context.activity)}`, { meta });

    const thinkingHandler = new ThinkingHandler(context, this.conversationHandler);

    const conversationId = context.activity.conversation.id;
    const conversationMessages = await this.conversationHandler.getConversationMessages(conversationId, meta);

    const replyStartTimestamp = await thinkingHandler.start(conversationMessages);

    const currentPrompt = this.promptGenerator.generateCurrentPrompt(context, meta);
    const fullPrompt = await this.generateFullPrompt(currentPrompt, conversationMessages, meta);
    const completionPayload = this.convertFullPromptToCompletionRequestPayload(fullPrompt, ragTenantId);

    logger.info('prompt to RAG', { prompt: fullPrompt, meta });
    let ragReply = await getRAGReply(completionPayload, ragOptions, meta);
    if (!ragReply) {
      ragReply = {
        id: 'N/A',
        answer: '⚠️Unable to establish a connection to the AI service at this time. Please try again later.',
        has_result: false,
        references: [],
      };
    }
    // TODO: try merge cancelTimer and stop into one method
    await thinkingHandler.safeCancelTimer();
    await thinkingHandler.stop(replyStartTimestamp, ragReply, currentPrompt);

    return { status: 'success' };
  }

  private convertFullPromptToCompletionRequestPayload(
    fullPrompt: MessageWithRemoteContent,
    tenantId: string
  ): CompletionRequestPayload {
    const message: Message = {
      role: 'user',
      content: fullPrompt.currentQuestion,
      name: fullPrompt.user,
    };

    const history: Message[] = [];
    fullPrompt.conversations.forEach((c) => {
      if (c.question) {
        history.push({ role: 'user', content: c.question, name: fullPrompt.user });
      }
      if (c.answer) {
        history.push({ role: 'assistant', content: c.answer });
      }
    });

    const additional_infos: AdditionalInfo[] = [];
    fullPrompt.additionalInfo.links.forEach((link) => {
      additional_infos.push({ type: 'link', content: link.text, link: link.url.toString() });
    });
    fullPrompt.additionalInfo.images.forEach((image) => {
      additional_infos.push({ type: 'image', content: image.text, link: image.url.toString() });
    });

    const payload: CompletionRequestPayload = {
      tenant_id: tenantId,
      message: message,
      history: history,
      additional_infos,
    };

    return payload;
  }

  private async generateFullPrompt(
    prompt: Prompt,
    messages: ConversationMessage[],
    meta: object
  ): Promise<MessageWithRemoteContent> {
    logger.info(`Add conversation messages to prompt`, { meta, messages: messages });
    const fullPrompt = await this.promptGenerator.generateFullPrompt(prompt, messages, meta);
    return fullPrompt;
  }
}
