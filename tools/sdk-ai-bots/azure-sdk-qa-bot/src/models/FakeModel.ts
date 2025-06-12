import { Memory, PromptCompletionModel, PromptFunctions, PromptTemplate, Tokenizer } from '@microsoft/teams-ai';
import { CardFactory, MessageFactory, TurnContext } from 'botbuilder';
import { getRAGReply, RAGOptions, RAGReply } from '../backend/rag.js';
import { createReplyCard } from '../cards/components/reply.js';
import { PromptResponse } from '@microsoft/teams-ai/lib/types/PromptResponse.js';
import { ThinkingHandler } from '../turn/ThinkingHandler.js';
import config from '../config/config.js';
import { PromptGenerator } from '../input/promptGenerator.js';
import { logger } from '../logging/logger.js';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { getRagTanent } from '../config/utils.js';
import { ConversationHandler, ConversationMessage, Prompt } from '../input/ConversationHandler.js';

export interface FakeModelOptions {
  rag: RAGOptions;
}

export class FakeModel implements PromptCompletionModel {
  private readonly urlRegex = /https?:\/\/[^\s"'<>]+/g;
  private readonly conversationHandler = new ConversationHandler();

  constructor() {
    this.conversationHandler.initialize();
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
    const ragTanentId = getRagTanent(channelId);
    const ragOptions: RAGOptions = {
      endpoint: config.ragEndpoint,
      apiKey: config.ragApiKey,
      tenantId: ragTanentId,
    };
    logger.info(`Received activity: ${JSON.stringify(context.activity)}`, { meta });

    const thinkingHandler = new ThinkingHandler(context);
    await thinkingHandler.start(context);

    const currentPrompt = this.generateCurrentPrompt(context, meta);
    const conversationId = context.activity.conversation.id;
    const plainPrompt = await this.generatePlainPromptIncludeConversationMessages(currentPrompt, conversationId, meta);
    logger.info('prompt to RAG:' + plainPrompt, { meta });
    let ragReply = await getRAGReply(plainPrompt, ragOptions, meta);
    if (!ragReply) {
      ragReply = { answer: 'AI Service is not available', has_result: false, references: [] };
    }
    // TODO: try merge cancelTimer and stop into one method
    thinkingHandler.cancelTimer();

    await this.saveCurrentConversationMessage(context, currentPrompt, ragReply, meta);
    await this.replyToUser(context, ragReply);

    await thinkingHandler.stop();

    return { status: 'success' };
  }

  // TODO: remove duplicate external link or image contents
  private async generatePlainPromptIncludeConversationMessages(
    prompt: Prompt,
    conversationId: string,
    meta: object
  ): Promise<string> {
    const conversationMessages = await this.conversationHandler.getConversationMessages(conversationId);
    logger.info(`Retrieved conversation messages for conversation ID: ${conversationId}`, {
      meta,
      messages: conversationMessages,
    });
    const promptGenerator = new PromptGenerator(meta);
    const fullPrompt = await promptGenerator.generatePlainFullPrompt(prompt, conversationMessages);
    return fullPrompt;
  }

  private async replyToUser(context: TurnContext, ragReply: RAGReply) {
    const card = createReplyCard(ragReply);
    const attachment = CardFactory.adaptiveCard(card);
    const replyCard = MessageFactory.attachment(attachment);
    await context.sendActivities([MessageFactory.text(ragReply.answer), replyCard]);
  }

  private generateCurrentPrompt(context: TurnContext, meta: object): Prompt {
    const removedMentionText = TurnContext.removeRecipientMention(context.activity);
    const text = context.activity.text;
    const inlineLinkUrls = text.match(this.urlRegex) || [];
    const attachmentUrls = (context.activity.attachments || [])
      .filter((attachment) => attachment.contentType === 'text/html' && attachment.content)
      .map((attachment) => attachment.content.match(this.urlRegex) || []);
    const uniqueLinksSet = new Set([...inlineLinkUrls, ...attachmentUrls.flat()]);
    const uniqueLinks = Array.from(uniqueLinksSet);
    logger.info(`Extracted links from activity`, { meta, uniqueLinks });

    const inlineImageUrls =
      context.activity.attachments
        ?.filter((attachment) => {
          return attachment.contentType && attachment.contentType.startsWith('image/');
        })
        .map((attachment) => attachment.contentUrl) ?? [];
    const rawPrompt: Prompt = {
      textWithoutMention: removedMentionText,
      links: uniqueLinks,
      images: inlineImageUrls,
      userName: context.activity.from.name,
      timestamp: context.activity.timestamp || new Date(),
    };
    logger.info(`Raw prompt generated: ${JSON.stringify(rawPrompt)}`, { meta });
    return rawPrompt;
  }

  private async saveCurrentConversationMessage(context: TurnContext, prompt: Prompt, reply: RAGReply, meta: object) {
    const currentMessage: ConversationMessage = {
      conversationId: context.activity.conversation.id,
      activityId: context.activity.id,
      prompt: prompt,
      reply: reply,
      // TODO: remove it
      timestamp: prompt.timestamp,
    };
    try {
      await this.conversationHandler.saveMessage(currentMessage);
    } catch (error) {
      logger.error('Failed to save current prompt', { error, meta });
    }
  }
}
