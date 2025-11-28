import { TurnContext } from 'botbuilder';
import { getUniqueLinks } from '../common/shared.js';
import { ConversationMessage, Prompt } from './ConversationHandler.js';
import { LinkContentExtractor } from './LinkContentExtractor.js';
import { RemoteContent } from './RemoteContent.js';
import { logger } from '../logging/logger.js';
import { LoggingAnalyzer } from './LoggingAnalyzer.js';

export interface MessageWithRemoteContent {
  user: string;
  currentQuestion: string;
  conversations: {
    question?: string;
    answer?: string;
  }[];
  additionalInfo: {
    links: RemoteContent[];
    images: RemoteContent[];
  };
}

export class PromptGenerator {
  private readonly urlRegex = /https?:\/\/[^\s"'<>]+/g;
  private linkContentExtractor = new LinkContentExtractor();
  private loggingAnalyzer = new LoggingAnalyzer();

  constructor() {}

  public async generateFullPrompt(
    prompt: Prompt,
    conversationMessages: ConversationMessage[],
    meta: object
  ): Promise<MessageWithRemoteContent> {
    const currentQuestion = prompt.textWithoutMention;
    const conversations = conversationMessages
      .filter((m) => m.prompt || m.reply)
      .map((m) => {
        const question = m.prompt ? m.prompt.textWithoutMention : undefined;
        const answer = m.reply ? m.reply.answer : undefined;
        return { question, answer };
      });
    const links = getUniqueLinks([
      ...(prompt.links || []),
      ...conversationMessages.flatMap((m) => m.prompt?.links || []),
    ]);
    const imagesSet = new Set<string>([
      ...(prompt.images || []),
      ...conversationMessages.flatMap((m) => m.prompt?.images || []),
    ]);
    const urls = links.map((link) => new URL(link));
    const isPipelineUrl = (url: URL) =>
      url.hostname.startsWith('dev.azure.com') && url.pathname.startsWith('/azure-sdk/public/_build');
    const pipelineUrls = urls.filter(isPipelineUrl);
    const nonPipelineUrls = urls.filter((url) => !isPipelineUrl(url));
    const linkContents: RemoteContent[][] = await Promise.all([
      this.linkContentExtractor.extract(nonPipelineUrls, meta),
      this.loggingAnalyzer.analyzePipelineLog(pipelineUrls, meta),
    ]);
    const additionalInfo = {
      links: linkContents.flat(),
      images: Array.from(imagesSet).map((image) => ({ text: '', id: '', url: new URL(image) })),
    };
    const user = prompt.userName || '';
    return { currentQuestion, conversations, additionalInfo, user };
  }

  public generateCurrentPrompt(context: TurnContext, meta: object): Prompt {
    const removedMentionText = TurnContext.removeRecipientMention(context.activity);
    const text = context.activity.text;
    const inlineLinkUrls = text.match(this.urlRegex) || [];
    const attachmentUrls = (context.activity.attachments || [])
      .filter((attachment) => attachment.contentType === 'text/html' && attachment.content)
      .map((attachment) => attachment.content.match(this.urlRegex) || []);
    const uniqueLinks = getUniqueLinks([...inlineLinkUrls, ...attachmentUrls.flat()]);
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
}
