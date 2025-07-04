import { getUniqueLinks } from '../common/shared.js';
import { ConversationMessage, Prompt } from './ConversationHandler.js';
import { LinkContentExtractor } from './LinkContentExtractor.js';
import { RemoteContent } from './RemoteContent.js';

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
  private meta: object;
  private linkContentExtractor: LinkContentExtractor;

  constructor(meta: object = {}) {
    this.meta = meta;
    this.linkContentExtractor = new LinkContentExtractor(this.meta);
  }

  public async generateFullPrompt(
    prompt: Prompt,
    conversationMessages: ConversationMessage[]
  ): Promise<MessageWithRemoteContent> {
    const currentQuestion = prompt.textWithoutMention;
    const conversations = conversationMessages
      .filter((m) => m.prompt || m.reply)
      .map((m) => {
        const question = m.prompt ? m.prompt.textWithoutMention : undefined;
        const answer = m.reply && m.reply.has_result ? m.reply.answer : undefined;
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
    const linkContents = await this.linkContentExtractor.extract(links.map((link) => new URL(link)));
    const additionalInfo = {
      links: linkContents,
      images: Array.from(imagesSet).map((image) => ({ text: '', id: '', url: new URL(image) })),
    };
    const user = prompt.userName || '';
    return { currentQuestion, conversations, additionalInfo, user };
  }
}
