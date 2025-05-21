import { Memory, PromptCompletionModel, PromptFunctions, PromptTemplate, Tokenizer } from '@microsoft/teams-ai';
import { CardFactory, MessageFactory, TurnContext } from 'botbuilder';
import { getRAGReply, RAGOptions, RAGReply } from '../backend/rag.js';
import { createReplyCard } from '../cards/components/reply.js';
import { PromptResponse } from '@microsoft/teams-ai/lib/types/PromptResponse.js';
import { ImageTextExtractor } from '../input/ImageContentExtractor.js';
import { ThinkingHandler } from '../turn/ThinkingHandler.js';
import { LinkContentExtractor } from '../input/LinkContentExtractor.js';
import config from '../config.js';
import { PromptGenerator } from '../input/promptGenerator.js';
import { logger } from '../logging/logger.js';
import { getTurnContextLogMeta } from '../logging/utils.js';

export interface FakeModelOptions {
  rag: RAGOptions;
}

export class FakeModel implements PromptCompletionModel {
  private readonly urlRegex = /https?:\/\/[^\s"'<>]+/g;
  private readonly options: FakeModelOptions;

  constructor(options: FakeModelOptions) {
    this.options = options;
  }

  public async completePrompt(
    context: TurnContext,
    memory: Memory,
    functions: PromptFunctions,
    tokenizer: Tokenizer,
    template: PromptTemplate
  ): Promise<PromptResponse<string>> {
    const meta = getTurnContextLogMeta(context);
    const thinkingHandler = new ThinkingHandler(context);
    await thinkingHandler.start(context);

    const prompt = await this.generatePrompt(context, meta);
    if (config.debug) {
      await context.sendActivity(`[DEBUG] Prompt: ${prompt}`);
    }
    logger.info('prompt to RAG:' + prompt, meta);
    let ragReply = await getRAGReply(prompt, this.options.rag, meta);
    if (!ragReply) {
      ragReply = { answer: 'AI Service is not available', has_result: false, references: [] };
    }
    // TODO: try merge cancelTimer and stop into one method
    thinkingHandler.cancelTimer();
    await this.replyToUser(context, ragReply);

    await thinkingHandler.stop();
    return { status: 'success' };
  }

  private async replyToUser(context: TurnContext, ragReply: RAGReply) {
    const card = createReplyCard(ragReply);
    const attachment = CardFactory.adaptiveCard(card);
    const replyCard = MessageFactory.attachment(attachment);
    await context.sendActivities([MessageFactory.text(ragReply.answer), replyCard]);
  }

  private async generatePrompt(context: TurnContext, meta: object): Promise<string> {
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
}
