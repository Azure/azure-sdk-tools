import { Memory, PromptCompletionModel, PromptFunctions, PromptTemplate, Tokenizer } from '@microsoft/teams-ai';
import { CardFactory, MessageFactory, TurnContext } from 'botbuilder';
import { getRAGReply, RAGOptions, RAGReply } from '../backend/rag.js';
import { createReplyCard } from '../cards/components/reply.js';
import { PromptResponse } from '@microsoft/teams-ai/lib/types/PromptResponse.js';
import { ImageTextExtractor } from '../input/ImageContentExtractor.js';
import { ThinkingHandler } from '../turn/ThinkingHandler.js';
import { LinkContentExtractor } from '../input/LinkContentExtractor.js';
import config from '../config/config.js';
import { PromptGenerator } from '../input/promptGenerator.js';
import { logger } from '../logging/logger.js';
import { getTurnContextLogMeta } from '../logging/utils.js';
import { getRagTanent } from '../config/utils.js';
import { ConversationHandler, ConversationMessage } from '../input/ConversationHandler.js';

export interface FakeModelOptions {
  rag: RAGOptions;
}

export class FakeModel implements PromptCompletionModel {
  private readonly urlRegex = /https?:\/\/[^\s"'<>]+/g;
  private readonly converationHandler = new ConversationHandler();

  constructor() {
    this.converationHandler.initialize();
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
    logger.info(`Processing request for channel: ${channelId}`, meta);
    const ragTanentId = getRagTanent(channelId);
    const ragOptions: RAGOptions = {
      endpoint: config.ragEndpoint,
      apiKey: config.ragApiKey,
      tenantId: ragTanentId,
    };
    const thinkingHandler = new ThinkingHandler(context);
    await thinkingHandler.start(context);

    const previousConversation = await this.getConversation(context, meta);
    const currentPrompt = await this.generatePromptFromTurnContext(context, meta);

    const previousQAs = previousConversation.sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime());
    const prompt = previousQAs.map((qa) => qa.text).join('\n\n') + `\n\n# Current Question:\n${currentPrompt}`;
    logger.info('prompt to RAG:' + prompt, meta);
    let ragReply = await getRAGReply(prompt, ragOptions, meta);
    if (!ragReply) {
      ragReply = { answer: 'AI Service is not available', has_result: false, references: [] };
    }
    // TODO: try merge cancelTimer and stop into one method
    thinkingHandler.cancelTimer();
    await this.replyToUser(context, ragReply);

    await thinkingHandler.stop();

    await this.saveCurrentQA(context, currentPrompt, ragReply, meta);

    return { status: 'success' };
  }
  private generateCurrentQA(prompt: string, answer: RAGReply, timestamp: Date) {
    const refences = answer.references
      .map((ref) => {
        return `
### reference
${ref.title}
### link
${ref.link}
### content
${ref.content}
### source
${ref.source}`;
      })
      .join('\n');

    return `
# Previous Conversation on ${timestamp}:
## Question
${prompt}
## Answer
${answer.answer}
## References
${refences}
## Has Result from RAG?
${answer.has_result ? 'Yes' : 'No'}
`;
  }

  private async replyToUser(context: TurnContext, ragReply: RAGReply) {
    const card = createReplyCard(ragReply);
    const attachment = CardFactory.adaptiveCard(card);
    const replyCard = MessageFactory.attachment(attachment);
    await context.sendActivities([MessageFactory.text(ragReply.answer), replyCard]);
  }

  private async generatePromptFromTurnContext(context: TurnContext, meta: object): Promise<string> {
    logger.info('Received activity:', JSON.stringify(context.activity), meta);
    const linkContentExtractor = new LinkContentExtractor(meta);
    const imageContentExtractor = new ImageTextExtractor(meta);
    const promptGenerator = new PromptGenerator(meta);

    const removedMentionText = TurnContext.removeRecipientMention(context.activity);
    const text = context.activity.text;
    const inlineLinkUrls = text.match(this.urlRegex)?.map((link) => new URL(link)) || [];
    const inlineImageUrls =
      context.activity.attachments
        ?.filter((attachment) => {
          return attachment.contentType && attachment.contentType.startsWith('image/');
        })
        .map((attachment) => new URL(attachment.contentUrl)) ?? [];

    const extractImageContentsPromise = imageContentExtractor.extract(inlineImageUrls);
    const extractLinkContentsPromise = linkContentExtractor.extract(inlineLinkUrls);

    const [imageContents, linkContents] = await Promise.all([extractImageContentsPromise, extractLinkContentsPromise]);
    const userName = context.activity.from.name;
    const prompt = promptGenerator.generate(userName, removedMentionText, imageContents, linkContents);
    return prompt;
  }

  private async getConversation(context: TurnContext, meta: object) {
    try {
      const conversation = await this.converationHandler.getConversationMessages(context.activity.conversation.id);
      return conversation;
    } catch (error) {
      logger.error('Failed to retrieve conversation messages', error, meta);
      return [];
    }
  }

  private async saveCurrentQA(context: TurnContext, currentQuestion: string, currentAnswer: RAGReply, meta: object) {
    const now = new Date();
    const qa = this.generateCurrentQA(currentQuestion, currentAnswer, now);
    const currentMessage: ConversationMessage = {
      conversationId: context.activity.conversation.id,
      activityId: context.activity.id,
      text: qa,
      timestamp: now,
    };
    try {
      await this.converationHandler.saveMessage(currentMessage);
    } catch (error) {
      logger.error('Failed to save current prompt', error, meta);
    }
  }
}
