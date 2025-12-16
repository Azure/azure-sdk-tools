import { CardFactory, MessageFactory, TurnContext } from 'botbuilder';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { ConversationHandler, ConversationMessage, Prompt, RAGReply } from '../input/ConversationHandler.js';
import { createContactCard } from '../cards/components/contact.js';
import { contactCardVersion } from '../config/config.js';
import { CompletionResponsePayload, isCompletionResponsePayload, RagApiError } from '../backend/rag.js';
import { logger } from '../logging/logger.js';
import { setTimeout } from 'node:timers/promises';

export class ThinkingHandler {
  private readonly thinkEmojis = ['⏳', '🤔', '💭', '🧠', '🤩', '🧐', '🚨', '🤭'];
  private readonly defaultThinkingMessage = '⏳Thinking';
  private readonly maxRetryTimesForFinish = 5;
  private readonly maxRetryTimesForThinking = 1800;
  private readonly maxCancelTimeout = 1000; // unit in milliseconds
  private readonly defaultThinkingInterval = 5000; // unit in milliseconds
  private readonly context: TurnContext;
  private readonly conversationHandler: ConversationHandler;
  private thinkingMessage = this.defaultThinkingMessage;
  private shouldStop = false;
  private isRunning = false;
  private resourceId: string | undefined = undefined;
  private meta: object;

  constructor(context: TurnContext, conversationHandler: ConversationHandler) {
    this.context = context;
    this.meta = getTurnContextLogMeta(context);
    this.conversationHandler = conversationHandler;
  }

  public async start(context: TurnContext, conversationMessages: ConversationMessage[]): Promise<Date> {
    await this.trySendContactCard(context, conversationMessages);
    const resource = await context.sendActivity(this.thinkingMessage);
    const timestamp = new Date();
    this.resourceId = resource.id;
    this.startCore(context, resource.id);
    return timestamp;
  }

  // cancel timer as soon as possible when get reply
  public async safeCancelTimer() {
    this.shouldStop = true;
    if (!this.isRunning) return;
    let retryCount = 0;
    for (; retryCount < this.maxRetryTimesForFinish; retryCount++) {
      if (!this.isRunning) break;
      await setTimeout(this.maxCancelTimeout);
    }
    if (retryCount === this.maxRetryTimesForFinish) {
      throw new Error('Failed to stop thinking timer');
    }
  }

  // separate this method from cancelTimer to make sure complete message is always shown
  public async stop(replyStartTime: Date, reply: CompletionResponsePayload | RagApiError, currentPrompt: Prompt) {
    const answer = this.generateAnswer(reply);
    const updated: Partial<TurnContext> = {
      type: 'message',
      id: this.resourceId,
      text: answer,
      conversation: this.context.activity.conversation,
    } as any;
    const response = await this.context.updateActivity(updated);
    if (response) {
      await this.saveCurrentConversationMessage(
        this.context.activity.conversation.id,
        this.context.activity.id,
        response.id,
        currentPrompt,
        reply,
        replyStartTime,
        this.meta
      );
    }
  }

  private generateAnswer(reply: CompletionResponsePayload | RagApiError) {
    if (!isCompletionResponsePayload(reply)) {
      const shouldRetryLater = reply.code === 'LLM_SERVICE_FAILURE' || reply.code === 'SEARCH_FAILURE';
      const retryMessage = shouldRetryLater ? ' Please try again later.' : '';
      const errorReply = `🚫Sorry, I'm having some ${reply.category} issues right now and can't answer your question.${retryMessage} Error: ${reply.message}.`;
      return errorReply;
    }

    // received reply successfully
    const answerWithReferences = this.addReferencesToReply(reply);
    return this.addEndingText(answerWithReferences);
  }

  private async startCore(context: TurnContext, resourceId: string) {
    let count = 0;
    for (; count < this.maxRetryTimesForThinking; count++) {
      if (this.shouldStop || this.isRunning) break;
      const updated: Partial<TurnContext> = {
        type: 'message',
        id: resourceId,
        text: this.updateThinkingMessage(),
        conversation: context.activity.conversation,
      } as any;
      try {
        this.isRunning = true;
        await context.updateActivity(updated);
      } finally {
        this.isRunning = false;
      }
      await setTimeout(this.defaultThinkingInterval);
    }
    if (count === this.maxRetryTimesForThinking) {
      logger.warn('Thinking timer reached max retry times', { meta: this.meta });
      await context.sendActivity('Thinking is taking too long, please try again later.');
    }
  }

  private updateThinkingMessage() {
    const index = getRandomInt(0, this.thinkEmojis.length - 1);
    const start = this.thinkingMessage.indexOf('Thi');
    this.thinkingMessage = this.thinkEmojis[index] + this.thinkingMessage.substring(start) + '.';
    return this.thinkingMessage;

    function getRandomInt(min: number, max: number) {
      min = Math.ceil(min);
      max = Math.floor(max);
      return Math.floor(Math.random() * (max - min + 1)) + min;
    }
  }

  private addReferencesToReply(ragReply: CompletionResponsePayload): string {
    let reply = ragReply.answer;
    if (ragReply.references.length === 0) return reply;

    // remove duplicate references
    const referencesMap = new Map<string, Map<string, string>>();
    ragReply.references?.forEach((ref) => {
      const map = referencesMap.get(ref.source) ?? new Map<string, string>();
      let url = undefined;
      try {
        url = new URL(ref.link);
      } catch (e) {
        logger.warn(`Invalid URL in reference: ${ref.link}`, { meta: this.meta });
        return;
      }
      map.set(url.href, ref.title);
      referencesMap.set(ref.source, map);
    });

    const prettierSource = (source: string) => {
      return source
        .split('_')
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
        .join(' ');
    };
    reply += '\n\n**References**\n';
    referencesMap.forEach((links, source) => {
      const sourceName = prettierSource(source);
      links.forEach((title, link) => {
        reply += `- [${title} | ${sourceName}](${link})\n`;
      });
    });
    return reply;
  }

  private addEndingText(answer: string): string {
    return answer + '\n\n> **NOTE:** If you have follow-up questions after my response, please @Azure SDK Q&A Bot to continue the conversation.';
  }

  private async trySendContactCard(context: TurnContext, conversationMessages: ConversationMessage[]) {
    const hasContactCard = conversationMessages.find((msg) => msg.contactCard);
    if (!hasContactCard) {
      const card = createContactCard();
      const attachment = CardFactory.adaptiveCard(card);
      const replyCard = MessageFactory.attachment(attachment);
      const response = await context.sendActivity(replyCard);
      if (response) {
        const contactCardMessage: ConversationMessage = {
          conversationId: context.activity.conversation.id,
          activityId: response.id,
          contactCard: {
            version: contactCardVersion,
          },
          timestamp: new Date(),
        };
        await this.conversationHandler.saveMessage(contactCardMessage, this.meta);
      }
    }
  }

  private async saveCurrentConversationMessage(
    conversationId: string,
    promptActivityId: string,
    replyActivityId: string,
    prompt: Prompt,
    replyPayload: CompletionResponsePayload | RagApiError,
    replyTimeStamp: Date,
    meta: object
  ) {
    const reply = this.convertPayloadToReply(replyPayload);
    const promptMessage: ConversationMessage = {
      conversationId,
      activityId: promptActivityId,
      prompt,
      timestamp: prompt.timestamp,
    };
    const replyMessage: ConversationMessage = {
      conversationId,
      activityId: replyActivityId,
      reply,
      timestamp: replyTimeStamp,
    };
    try {
      await Promise.all([
        this.conversationHandler.saveMessage(promptMessage, meta),
        this.conversationHandler.saveMessage(replyMessage, meta),
      ]);
    } catch (error) {
      logger.error('Failed to save current prompt', { error, meta });
    }
  }

  private convertPayloadToReply(replyPayload: CompletionResponsePayload | RagApiError) {
    if (!isCompletionResponsePayload(replyPayload)) {
      const answer = this.generateAnswer(replyPayload);
      return {
        answer,
        has_result: false,
        references: [],
      };
    }
    return {
      answer: replyPayload.answer,
      has_result: replyPayload.has_result,
      references: replyPayload.references?.map((ref) => ({ ...ref })) || [],
    };
  }
}
