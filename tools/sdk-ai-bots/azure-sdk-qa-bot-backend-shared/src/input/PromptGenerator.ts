import { logger } from '../logging/logger.js';
import { RemoteContent } from './RemoteContent.js';

export class PromptGenerator {
  private logMeta: object;

  constructor(logMeta: object = {}) {
    this.logMeta = logMeta;
  }

  generate(user?: string, textInput?: string, imageContents?: RemoteContent[], linkContents?: RemoteContent[]) {
    const createSection = (content: RemoteContent) => {
      if (content.error) {
        logger.warn(`Skip remote content due to error: ${content.error}`, { meta: this.logMeta });
      }
      return `### Content from ${content.id}: ${content.url}\n\n${content.text}\n\n`;
    };
    const imagesPrompt = imageContents
      ? `## Additional information from images\n\n${imageContents.map(createSection)}\n\n`
      : ``;
    const linksPrompt = linkContents
      ? `## Additional information from links\n\n${linkContents.map(createSection)}\n\n`
      : ``;

    return `
# Question from user ${user ?? ''}

${textInput}

${imagesPrompt}

${linksPrompt}

`;
  }
}
