import { Memory, PromptCompletionModel, PromptFunctions, PromptTemplate, Tokenizer } from '@microsoft/teams-ai';
import { TurnContext } from 'botbuilder';
import {
  AdditionalInfo,
  CompletionRequestPayload,
  CompletionResponsePayload,
  getRAGReply,
  Message,
  RAGOptions,
} from '../backend/rag.js';
import { PromptResponse } from '@microsoft/teams-ai/lib/types/PromptResponse.js';
import { ThinkingHandler } from '../turn/ThinkingHandler.js';
import config from '../config/config.js';
import { MessageWithRemoteContent, PromptGenerator } from '../input/PromptGeneratorV2.js';
import { logger } from '../logging/logger.js';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { getRagTanent as getRagTetant } from '../config/utils.js';
import { ConversationHandler, ConversationMessage, Prompt, RAGReply } from '../input/ConversationHandler.js';

export class RAGModel implements PromptCompletionModel {
  private readonly conversationHandler: ConversationHandler;
  private readonly promptGenerator: PromptGenerator;

  constructor(conversationHandler: ConversationHandler) {
    this.conversationHandler = conversationHandler;
    this.promptGenerator = new PromptGenerator();
  }

  public async completePrompt(
    context: TurnContext,
    memory: Memory,
    functions: PromptFunctions,
    tokenizer: Tokenizer,
    template: PromptTemplate
  ): Promise<PromptResponse<string>> {
    const meta = getTurnContextLogMeta(context);
    const channelId = context.activity.conversation.id.split(';')[0];
    logger.info(`Processing request for channel: ${channelId}`, { meta });
    const ragTanentId = getRagTetant(channelId);
    const ragOptions: RAGOptions = {
      endpoint: config.ragEndpoint,
      apiKey: config.ragApiKey,
    };
    logger.info(`Received activity: ${JSON.stringify(context.activity)}`, { meta });

    const thinkingHandler = new ThinkingHandler(context, this.conversationHandler);

    const conversationId = context.activity.conversation.id;
    const conversationMessages = await this.conversationHandler.getConversationMessages(conversationId, meta);

    await thinkingHandler.start(context, conversationMessages);

    const currentPrompt = this.promptGenerator.generateCurrentPrompt(context, meta);
    const fullPrompt = await this.generateFullPrompt(currentPrompt, conversationMessages, meta);
    const completionPayload = this.convertFullPromptToCompletionRequestPayload(fullPrompt, ragTanentId);

    logger.info('prompt to RAG', { prompt: fullPrompt, meta });
    let ragReply = await getRAGReply(completionPayload, ragOptions, meta);
    if (!ragReply) {
      ragReply = { id: 'N/A', answer: 'AI service is not available', has_result: false, references: [] };
    }
    // TODO: try merge cancelTimer and stop into one method
    thinkingHandler.cancelTimer();
    await this.saveCurrentConversationMessage(context, currentPrompt, ragReply, meta);
    await thinkingHandler.stop(ragReply);

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

  private async saveCurrentConversationMessage(
    context: TurnContext,
    prompt: Prompt,
    replyPayload: CompletionResponsePayload,
    meta: object
  ) {
    const reply: RAGReply = {
      answer: replyPayload.answer,
      has_result: replyPayload.has_result,
      references: replyPayload.references.map((ref) => ({ ...ref })) || [],
    };
    const currentMessage: ConversationMessage = {
      conversationId: context.activity.conversation.id,
      activityId: context.activity.id,
      prompt,
      reply,
      timestamp: prompt.timestamp,
    };
    try {
      await this.conversationHandler.saveMessage(currentMessage, meta);
    } catch (error) {
      logger.error('Failed to save current prompt', { error, meta });
    }
  }
}
