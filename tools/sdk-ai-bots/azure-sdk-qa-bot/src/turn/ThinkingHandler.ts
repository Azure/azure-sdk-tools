import { Activity, CardFactory, MessageFactory, TurnContext } from 'botbuilder';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { ConversationHandler, ConversationMessage, Prompt, RAGReply } from '../input/ConversationHandler.js';
import { createContactCard } from '../cards/components/contact.js';
import { contactCardVersion } from '../config/config.js';
import { TenantConfigManager, KnownTenants } from '../config/tenant.js';
import { CompletionResponsePayload, isCompletionResponsePayload, RagApiError } from '../backend/rag.js';
import { logger } from '../logging/logger.js';
import { sendActivityWithRetry } from '../activityUtils.js';
import type { AIEntity } from '@microsoft/teams-ai/lib/types/AIEntity.js';
import { getTeamsMentionEntities } from '../common/mentions.js';

export class ThinkingHandler {
  private readonly aiGeneratedEntity: AIEntity = {
    type: 'https://schema.org/Message',
    '@type': 'Message',
    '@context': 'https://schema.org',
    '@id': '',
    additionalType: ['AIGeneratedContent'],
  };
  private readonly context: TurnContext;
  private readonly conversationHandler: ConversationHandler;
  private readonly tenantConfigManager: TenantConfigManager;
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

  public async prepare(conversationMessages: ConversationMessage[]): Promise<Date> {
    await this.trySendContactCard(conversationMessages);
    return new Date();
  }

  public async sendReply(replyStartTime: Date, reply: CompletionResponsePayload | RagApiError, currentPrompt: Prompt, currentChannelTenant?: string) {
    const { answer, isError } = this.generateAnswer(reply);
    const routeTenant = isCompletionResponsePayload(reply) ? reply.route_tenant : undefined;
    const traceId = isCompletionResponsePayload(reply) ? reply.trace_id : undefined;
    const formattedAnswer = await this.formatAnswer(answer, isError, routeTenant, currentChannelTenant);
    const mentions = getTeamsMentionEntities(
      answer,
      isCompletionResponsePayload(reply) ? reply.mentions : undefined
    );
    const entity: AIEntity = {
      ...this.aiGeneratedEntity,
    };
    if (traceId) {
      entity.usageInfo = {
        type: 'https://schema.org/Message',
        '@type': 'CreativeWork',
        name: 'Internal Tracking',
        description: `Trace ID: ${traceId}`,
      };
    }
    const activity: Partial<Activity> = {
      type: 'message',
      text: formattedAnswer,
      entities: [entity, ...mentions],
      conversation: this.context.activity.conversation,
    };

    const response = await sendActivityWithRetry(this.context, activity);
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

  private generateAnswer(reply: CompletionResponsePayload | RagApiError): { answer: string; isError: boolean } {
    if (!isCompletionResponsePayload(reply)) {
      const shouldRetryLater = reply.code === 'LLM_SERVICE_FAILURE' || reply.code === 'SEARCH_FAILURE';
      const retryMessage = shouldRetryLater ? ' Please try again later.' : '';
      const errorReply = `🚫Sorry, I'm having some ${reply.category} issues right now and can't answer your question.${retryMessage} Error: ${reply.message}.`;
      return { answer: errorReply, isError: true };
    }

    // received reply successfully
    const answerWithReferences = this.addReferencesToReply(reply);
    return { answer: answerWithReferences, isError: false };
  }

  /**
   * Format the answer with conditional footer based on route_tenant.
   * For error responses, returns the answer as-is without footer.
   */
  private async formatAnswer(answer: string, isError: boolean, routeTenant?: string, currentChannelTenant?: string): Promise<string> {
    // For error responses, return plain text without footer
    if (isError) {
      return answer;
    }

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

    if (footerParts.length === 0) {
      return answer;
    }

    return `${answer}\n\n---\n\n${footerParts.join('\n\n')}`;
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
