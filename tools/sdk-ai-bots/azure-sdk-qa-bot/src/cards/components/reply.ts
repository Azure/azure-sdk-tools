import { createFeedbackCard } from './feedback.js';
import { CompletionResponsePayload } from '../../backend/rag.js';
import { createReferencesListCard } from './reference-list.js';
import { supportChannelCard } from './support-channel.js';
import { RAGReference } from '../../input/ConversationHandler.js';

export function createReplyCard(reply: CompletionResponsePayload, conversationId: string, activity: string) {
  const referenceDataList = reply.references.map((ref) => ({
    title: ref.title,
    sourceName: ref.link.split('/').pop() || '',
    sourceUrl: ref.link,
    excerpt: ref.content,
  }));
  const referenceListCard = createReferencesListCard(referenceDataList);
  const referenceAction = {
    type: 'Action.ShowCard',
    title: '📑References📑',
    card: referenceListCard,
  };
  const feedbackCard = createFeedbackCard(conversationId, activity);
  const feedbackAction = {
    type: 'Action.ShowCard',
    title: '👍Feedback👎',
    card: feedbackCard,
  };
  const supportChannelAction = {
    type: 'Action.ShowCard',
    title: '🕵️‍♂️Support Channels🕵️‍♀️',
    card: supportChannelCard,
  };
  const actions =
    referenceDataList.length > 0
      ? [referenceAction, feedbackAction, supportChannelAction]
      : [feedbackAction, supportChannelAction];
  const card = {
    type: 'AdaptiveCard',
    // adaptive card does not support FULL markdown in attachment, use message instead
    body: [],
    actions,
    $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
    version: '1.6',
  };
  return card;
}
