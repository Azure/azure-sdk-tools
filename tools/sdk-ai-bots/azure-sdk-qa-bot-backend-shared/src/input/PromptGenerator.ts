import { logger } from '../logging/logger.js';
import { RemoteContent } from './RemoteContent.js';

export class PromptGenerator {
  private logMeta: object;

  constructor(logMeta: object = {}) {
    this.logMeta = logMeta;
  }

  generate(user?: string, textInput?: string, imageContents?: RemoteContent[], linkContents?: RemoteContent[]) {
    const imagesPrompt = imageContents
      ? `## Additional information from images\n\n${imageContents.map(this.createSection)}\n\n`
      : ``;
    const linksPrompt = linkContents
      ? `## Additional information from links\n\n${linkContents.map(this.createSection)}\n\n`
      : ``;

    return `
# Question from user ${user ?? ''}

${textInput}

${imagesPrompt}

${linksPrompt}

`;
  }

  private createSection(content: RemoteContent) {
    if (content.error) {
      logger.warn(`Skip remote content due to error: ${content.error}`, this.logMeta);
    }
    return `### Content from ${content.id}: ${content.url}\n\n${content.text}\n\n`;
  }
}
