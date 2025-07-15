import { CardFactory, MessageFactory, TurnContext } from 'botbuilder';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { ConversationHandler, ConversationMessage } from '../input/ConversationHandler.js';
import { createContactCard } from '../cards/components/contact.js';
import { contactCardVersion } from '../config/config.js';
import { CompletionResponsePayload } from '../backend/rag.js';
import { logger } from '../logging/logger.js';

export class ThinkingHandler {
  private readonly thinkEmojis = ['⏳', '🤔', '💭', '🧠', '🤩', '🧐', '🚨', '🤭'];
  private readonly defaultThinkingMessage = '⏳Thinking';

  private readonly context: TurnContext;
  private readonly conversationHandler: ConversationHandler;
  private thinkingMessage = this.defaultThinkingMessage;
  private isThinking = true;
  private timerHandler: NodeJS.Timeout | undefined = undefined;
  private resourceId: string | undefined = undefined;
  private meta: object;

  constructor(context: TurnContext, conversationHandler: ConversationHandler) {
    this.context = context;
    this.meta = getTurnContextLogMeta(context);
    this.conversationHandler = conversationHandler;
  }

  public async start(context: TurnContext, conversationMessages: ConversationMessage[]): Promise<void> {
    await this.trySendContactCard(context, conversationMessages);

    const resource = await context.sendActivity(this.thinkingMessage);
    this.timerHandler = setInterval(async () => {
      const updated: Partial<TurnContext> = {
        type: 'message',
        id: resource.id,
        text: this.updateThinkingMessage(),
        conversation: context.activity.conversation,
      } as any;
      if (this.isThinking) {
        await context.updateActivity(updated);
      }
    }, 1000);
    this.resourceId = resource.id;
  }

  // cancel timer as soon as possible when get reply
  public cancelTimer() {
    this.isThinking = false;
    if (this.timerHandler) clearInterval(this.timerHandler);
  }

  // separate this method from cancelTimer to make sure complete message is always shown
  public async stop(reply: CompletionResponsePayload) {
    const answer = this.addReferencesToReply(reply);
    const updated: Partial<TurnContext> = {
      type: 'message',
      id: this.resourceId,
      text: answer,
      conversation: this.context.activity.conversation,
    } as any;
    await this.context.updateActivity(updated);
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
      reply += `- ${prettierSource(source)}\n`;
      links.forEach((title, link) => {
        reply += `  - [${title}](${link})\n`;
      });
    });
    return reply;
  }

  private async trySendContactCard(context: TurnContext, conversationMessages: ConversationMessage[]) {
    const hasContactCard = conversationMessages.find((msg) => msg.contactCard);
    if (!hasContactCard) {
      const card = createContactCard(context.activity.conversation.id, context.activity.id);
      const attachment = CardFactory.adaptiveCard(card);
      const replyCard = MessageFactory.attachment(attachment);
      await context.sendActivity(replyCard);
      const contactCardMessage: ConversationMessage = {
        conversationId: context.activity.conversation.id,
        activityId: context.activity.id,
        contactCard: {
          resourceId: replyCard.id,
          version: contactCardVersion,
        },
        timestamp: new Date(),
      };
      await this.conversationHandler.saveMessage(contactCardMessage, this.meta);
    }
  }
}
