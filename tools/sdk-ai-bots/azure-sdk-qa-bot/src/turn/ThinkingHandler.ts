import { Activity, CardFactory, Mention, MessageFactory, TurnContext } from 'botbuilder';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { ConversationHandler, ConversationMessage, Prompt, RAGReply } from '../input/ConversationHandler.js';
import { createContactCard } from '../cards/components/contact.js';
import { contactCardVersion } from '../config/config.js';
import { TenantConfigManager, KnownTenants } from '../config/tenant.js';
import { CompletionResponsePayload, isCompletionResponsePayload, RagApiError } from '../backend/rag.js';
import type { ChannelBotSettings } from '../config/channel.js';
import { logger } from '../logging/logger.js';
import { setTimeout } from 'node:timers/promises';
import { sendActivityWithRetry, updateActivityWithRetry } from '../activityUtils.js';
import type { AIEntity } from '@microsoft/teams-ai/lib/types/AIEntity.js';

export class ThinkingHandler {
  private readonly thinkEmojis = ['⏳', '🤔', '💭', '🧠', '🤩', '🧐', '🚨', '🤭'];
  private readonly defaultThinkingMessage = '⏳Thinking';
  private readonly aiGeneratedEntity: AIEntity = {
    type: 'https://schema.org/Message',
    '@type': 'Message',
    '@context': 'https://schema.org',
    '@id': '',
    additionalType: ['AIGeneratedContent'],
  };
  private readonly maxRetryTimesForFinish = 5;
  private readonly maxRetryTimesForThinking = 1800;
  private readonly maxCancelTimeout = 1000; // unit in milliseconds
  private readonly defaultThinkingInterval = 5000; // unit in milliseconds
  private readonly context: TurnContext;
  private readonly conversationHandler: ConversationHandler;
  private readonly tenantConfigManager: TenantConfigManager;
  private thinkingMessage = this.defaultThinkingMessage;
  private shouldStop = false;
  private isRunning = false;
  private resourceId: string | undefined = undefined;
  private meta: object;

  constructor(
    context: TurnContext,
    conversationHandler: ConversationHandler,
    tenantConfigManager: TenantConfigManager
  ) {
    this.context = context;
    this.meta = getTurnContextLogMeta(context);
    this.conversationHandler = conversationHandler;
    this.tenantConfigManager = tenantConfigManager;
  }

  public async start(conversationMessages: ConversationMessage[]): Promise<Date> {
    await this.trySendContactCard(conversationMessages);
    const resource = await sendActivityWithRetry(this.context, this.thinkingMessage);
    const timestamp = new Date();
    this.resourceId = resource.id;
    this.startCore(resource.id);
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

  public async stop(replyStartTime: Date, reply: CompletionResponsePayload | RagApiError, currentPrompt: Prompt, currentChannelTenant?: string, botSettings?: ChannelBotSettings) {
    const completion = isCompletionResponsePayload(reply) ? reply : undefined;
    try {
      try {
        await this.safeCancelTimer();
      } catch (error) {
        logger.warn('Failed to stop Thinking timer; proceeding with reply delivery', { error, meta: this.meta });
      }
      const { answer, mentions } = this.generateAnswer(reply, currentChannelTenant, botSettings);
      const entity: AIEntity = {
        ...this.aiGeneratedEntity,
      };
      if (completion?.trace_id) {
        entity.usageInfo = {
          type: 'https://schema.org/Message',
          '@type': 'CreativeWork',
          name: 'Internal Tracking',
          description: `Trace ID: ${completion.trace_id}`,
        };
      }
      const activity: Partial<Activity> = {
        type: 'message',
        id: this.resourceId,
        text: answer,
        entities: [entity, ...mentions],
        conversation: this.context.activity.conversation,
      };

      const response = await updateActivityWithRetry(this.context, activity);
      if (!response || !response.id?.trim()) {
        logger.error('No Teams activity ID for reply; not retrying', { meta: this.meta });
        return;
      }
      await this.saveCurrentConversationMessage(
        this.context.activity.conversation.id,
        this.context.activity.id,
        response.id,
        currentPrompt,
        reply,
        replyStartTime,
        this.meta
      );
    } catch (error) {
      logger.error('Unable to finish Teams reply; not posting another activity', { error, meta: this.meta });
    }
  }

  private generateAnswer(
    reply: CompletionResponsePayload | RagApiError,
    currentChannelTenant?: string,
    botSettings?: ChannelBotSettings
  ): { answer: string; mentions: Mention[] } {
    if (!isCompletionResponsePayload(reply)) {
      const shouldRetryLater = reply.code === 'LLM_SERVICE_FAILURE' || reply.code === 'SEARCH_FAILURE';
      const retryMessage = shouldRetryLater ? ' Please try again later.' : '';
      const errorReply = `🚫Sorry, I'm having some ${reply.category} issues right now and can't answer your question.${retryMessage} Error: ${reply.message}.`;
      return { answer: errorReply, mentions: [] };
    }

    let confidence = reply.confidence;
    let notifyExperts = reply.notify_experts === true && botSettings?.allow_notify_experts === true;
    if ((reply.notify_experts !== undefined && typeof reply.notify_experts !== 'boolean') ||
        (confidence != null && (!['high', 'medium', 'low'].includes(confidence.level) ||
          typeof confidence.summary !== 'string' ||
          !Array.isArray(confidence.unresolved_needs) ||
          !confidence.unresolved_needs.every(need => typeof need === 'string') ||
          typeof confidence.needs_expert_help !== 'boolean'))) {
      logger.warn('Invalid confidence response; displaying the answer without confidence or mentions', { meta: this.meta });
      confidence = undefined;
      notifyExperts = false;
    }
    let answer = this.addReferencesToReply(reply);
    if (botSettings?.show_confidence_label && confidence) {
      const level = confidence.level[0].toUpperCase() + confidence.level.slice(1);
      answer = `**Confidence: ${level}**\n\n${answer}`;
    }
    if (confidence?.summary) answer += `\n\n${confidence.summary}`;
    if (confidence?.unresolved_needs.length) {
      answer += `\n\n**Unresolved needs**\n${confidence.unresolved_needs.map(need => `- ${need}`).join('\n')}`;
    }
    const routeTenant = reply.route_tenant;
    const footerParts: string[] = [];

    if (routeTenant) {
      try {
        const tenant = this.tenantConfigManager.getTenant(routeTenant);
        if (!tenant) {
          logger.warn(`Tenant not found for route_tenant: ${routeTenant}`, { meta: this.meta });
        } else {
          const displayName = tenant.channel_name || routeTenant;
          const channelLink = tenant.channel_link ? `[${displayName}](${tenant.channel_link})` : `${displayName}`;
          footerParts.push(`💬 Not resolved? Please re-post in the 👉 ${channelLink} channel where our domain experts can provide a deeper dive.`);
        }
      } catch (error) {
        logger.error(`Failed to get tenant info for route_tenant: ${routeTenant}`, { error: error.message, meta: this.meta });
      }
    }

    // Show TypeSpec skill promo when the effective tenant is the TypeSpec channel (or the default channel for backward compatibility)
    const effectiveTenant = routeTenant ?? currentChannelTenant;
    if (effectiveTenant === KnownTenants.TypeSpec || effectiveTenant === KnownTenants.Default) {
      footerParts.push(`🚀 **Try the Azure TypeSpec Author skill** to write API specifications in TypeSpec! Check out the Quick Start and samples [here](https://azure.github.io/typespec-azure/docs/getstarted/typespec-authoring-skill/).`);
    }

    const mentions: Mention[] = [];
    if (notifyExperts) {
      if (!botSettings.experts.length) {
        logger.warn('Expert notification authorized without recipients; displaying the answer without mentions', { meta: this.meta });
      } else {
        const escape = (text: string) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        mentions.push(...botSettings.experts.map(expert => ({
          type: 'mention',
          mentioned: { id: expert.id, name: expert.name },
          text: `<at>${escape(expert.name)}</at>`,
        })));
        answer += `\n\n${mentions.map(mention => mention.text).join(' ')}, could you help with the unresolved parts?`;
      }
    }
    if (footerParts.length) {
      answer += `\n\n---\n\n${footerParts.join('\n\n')}`;
    }
    return { answer, mentions };
  }

  private async startCore(resourceId: string) {
    let count = 0;
    for (; count < this.maxRetryTimesForThinking; count++) {
      if (this.shouldStop || this.isRunning) break;
      const updated: Partial<TurnContext> = {
        type: 'message',
        id: resourceId,
        text: this.updateThinkingMessage(),
        conversation: this.context.activity.conversation,
      } as any;
      try {
        this.isRunning = true;
        await updateActivityWithRetry(this.context, updated);
      } finally {
        this.isRunning = false;
      }
      await setTimeout(this.defaultThinkingInterval);
    }
    if (count === this.maxRetryTimesForThinking) {
      logger.warn('Thinking timer reached max retry times', { meta: this.meta });
      await sendActivityWithRetry(this.context, 'Thinking is taking too long, please try again later.');
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
    if (ragReply.references === null || ragReply.references === undefined || ragReply.references.length === 0) return reply;

    // remove duplicate references
    const referencesMap = new Map<string, Map<string, string>>();
    ragReply.references?.forEach((ref) => {
      const normalizedSource = (ref.source || '').trim();
      const map = referencesMap.get(normalizedSource) ?? new Map<string, string>();
      let url = undefined;
      try {
        url = new URL(ref.link);
      } catch (e) {
        logger.warn(`Invalid URL in reference: ${ref.link}`, { meta: this.meta });
        return;
      }
      map.set(url.href, ref.title);
      referencesMap.set(normalizedSource, map);
    });

    const prettierSource = (source: string) => {
      return source
        .split('_')
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
        .join(' ');
    };
    reply += '\n\n**References**\n';
    referencesMap.forEach((links, source) => {
      const sourceName = source ? prettierSource(source) : '';
      links.forEach((title, link) => {
        const referenceLabel = sourceName ? `${title} | ${sourceName}` : title;
        reply += `- [${referenceLabel}](${link})\n`;
      });
    });
    return reply;
  }

  private async trySendContactCard(conversationMessages: ConversationMessage[]) {
    const hasContactCard = conversationMessages.find((msg) => msg.contactCard);
    if (!hasContactCard) {
      const card = createContactCard();
      const attachment = CardFactory.adaptiveCard(card);
      const replyCard = MessageFactory.attachment(attachment);
      const response = await sendActivityWithRetry(this.context, replyCard);
      if (response) {
        const contactCardMessage: ConversationMessage = {
          conversationId: this.context.activity.conversation.id,
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
      const { answer } = this.generateAnswer(replyPayload);
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
