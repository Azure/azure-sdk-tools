import { RAGReply } from '../backend/rag.js';
import { logger } from '../logging/logger.js';
import { ConversationMessage, Prompt } from './ConversationHandler.js';
import { ImageTextExtractor } from './ImageContentExtractor.js';
import { LinkContentExtractor } from './LinkContentExtractor.js';
import { RemoteContent } from './RemoteContent.js';

export class PromptGenerator {
  private meta: object;
  private imageTextExtractor: ImageTextExtractor;
  private linkContentExtractor: LinkContentExtractor;

  constructor(meta: object = {}) {
    this.meta = meta;
    this.imageTextExtractor = new ImageTextExtractor(this.meta);
    this.linkContentExtractor = new LinkContentExtractor(this.meta);
  }

  public async generatePlainFullPrompt(prompt: Prompt, conversationMessages: ConversationMessage[]): Promise<string> {
    const [plainCurrentPrompt, plainConversations] = await Promise.all([
      this.generatePlainPrompt(prompt, 1),
      this.generatePlainConversations(conversationMessages),
    ]);

    return `${plainCurrentPrompt}

# Appendix: Previous conversations
${plainConversations}
`;
  }

  private async generatePlainConversations(conversationMessages: ConversationMessage[]): Promise<string> {
    const conversationPromises = conversationMessages.map((message) => {
      return Promise.all([
        message.prompt ? this.generatePlainPrompt(message.prompt) : new Promise((resolve) => resolve('')),
        message.reply ? this.generatePlainReply(message.reply) : new Promise((resolve) => resolve('')),
        new Promise((resolve) => resolve(message.timestamp)),
      ]);
    });
    const conversations = await Promise.all(conversationPromises);
    return conversations
      .map(([plainPrompt, plainReply, timestamp]) => {
        return `## Conversation question & answer on date: ${timestamp}
${plainPrompt}
${plainReply}`;
      })
      .join('\n');
  }

  private async generatePlainReply(reply: RAGReply): Promise<string> {
    const plainReferences = reply.references
      .map((ref) => {
        return `##### Title
${ref.title}
##### Source
${ref.source}
##### Link
${ref.link}
##### Content
${ref.content}`;
      })
      .join('\n');

    return `### AI Reply
#### Answer
${reply.answer}
#### Has result
${reply.has_result ? 'Yes' : 'No'}
#### References
${plainReferences}`;
  }

  private async generatePlainPrompt(prompt: Prompt, startHeadingLevel: number = 3): Promise<string> {
    const imageContents = await this.imageTextExtractor.extract(prompt.images.map((image) => new URL(image)));
    const linkContents = await this.linkContentExtractor.extract(prompt.links.map((link) => new URL(link)));

    const imagesPrompt = imageContents
      ? `${this.createHeadingLevel(startHeadingLevel + 1)} Additional information from images\n${imageContents
          .map((content) => this.createSection(content, startHeadingLevel + 2))
          .join('\n')}`
      : ``;
    const linksPrompt = linkContents
      ? `${this.createHeadingLevel(startHeadingLevel + 1)} Additional information from links\n${linkContents
          .map((content) => this.createSection(content, startHeadingLevel + 2))
          .join('\n')}`
      : ``;

    const plainPrompt = `${this.createHeadingLevel(startHeadingLevel)} Question from user ${prompt.userName} on date: ${
      prompt.timestamp
    }
${prompt.textWithoutMention}
${imagesPrompt}
${linksPrompt}`;

    return plainPrompt;
  }

  private createHeadingLevel(level: number): string {
    return `${'#'.repeat(level)}`;
  }

  private createSection(content: RemoteContent, headingLevel: number): string {
    if (content.error) {
      logger.warn(`Skip remote content due to error: ${content.error}`, { meta: this.meta });
    }
    return `${this.createHeadingLevel(headingLevel)} Content from ${content.id}: ${content.url}\n${content.text}`;
  }
}
