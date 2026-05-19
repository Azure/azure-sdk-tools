import { TurnContext } from 'botbuilder';
import { ConversationMessage, Prompt } from './ConversationHandler.js';
import { RemoteContent } from './RemoteContent.js';
import { logger } from '../logging/logger.js';

export interface MessageWithRemoteContent {
  userName: string;
  userID: string;
  currentQuestion: string;
  conversationID: string;
  additionalInfo: {
    images: RemoteContent[];
  };
}

export class PromptGenerator {

  public async generateFullPrompt(
    prompt: Prompt,
    conversationMessages: ConversationMessage[],
    meta: object
  ): Promise<MessageWithRemoteContent> {
    const currentQuestion = prompt.textWithoutMention;
    const imagesSet = new Set<string>([
      ...(prompt.images || []),
      ...conversationMessages.flatMap((m) => m.prompt?.images || []),
    ]);
    const additionalInfo = {
      images: Array.from(imagesSet).map((image) => ({ text: '', id: '', url: new URL(image) })),
    };
    const userName = prompt.userName || '';
    const userID = prompt.userID || '';
    return {
        currentQuestion: currentQuestion,
        additionalInfo: additionalInfo,
        userName: userName,
        userID: userID,
        conversationID: prompt.conversationID
    };
  }

  public generateCurrentPrompt(context: TurnContext, meta: object): Prompt {
    const removedMentionText = TurnContext.removeRecipientMention(context.activity);

    const inlineImageUrls =
      context.activity.attachments
        ?.filter((attachment) => {
          return attachment.contentType && attachment.contentType.startsWith('image/');
        })
        .map((attachment) => attachment.contentUrl) ?? [];
    const rawPrompt: Prompt = {
      textWithoutMention: removedMentionText,
      images: inlineImageUrls,
      userName: context.activity.from.name,
      userID: context.activity.from.id,
      timestamp: context.activity.timestamp ?? new Date(),
      conversationID: context.activity.conversation.id,
    };
    logger.info(`Raw prompt generated: ${JSON.stringify(rawPrompt)}`, { meta });
    return rawPrompt;
  }
}
