import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ReplyHandler } from '../src/turn/ReplyHandler.js';
import type { CompletionResponsePayload } from '../src/backend/rag.js';
import { logger } from '../src/logging/logger.js';

vi.mock('../src/activityUtils.js', () => ({
  sendActivityWithRetry: vi.fn((context, activity) => context.sendActivity(activity)),
}));
vi.mock('../src/config/config.js', () => ({ contactCardVersion: '1.0' }));
vi.mock('../src/logging/utils.js', () => ({ getTurnContextLogMeta: () => ({}) }));
vi.mock('../src/logging/logger.js', () => ({
  logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn() },
}));
vi.mock('../src/cards/components/contact.js', () => ({ createContactCard: vi.fn() }));

describe('confidence delivery', () => {
  let context: any;
  let handler: ReplyHandler;
  let saveMessage: ReturnType<typeof vi.fn>;
  let reply: CompletionResponsePayload;
  const prompt: any = { timestamp: new Date(), textWithoutMention: 'question' };
  const history: any[] = [{
    conversationId: 'channel;messageid=root',
    activityId: 'contact-card',
    contactCard: { version: '1.0' },
    timestamp: new Date(),
  }];

  beforeEach(() => {
    vi.clearAllMocks();
    context = {
      activity: { id: 'original', conversation: { id: 'channel;messageid=root' } },
      sendActivity: vi.fn().mockResolvedValue({ id: 'reply-id' }),
    };
    saveMessage = vi.fn().mockResolvedValue(undefined);
    handler = new ReplyHandler(
      context,
      { saveMessage } as any,
      { getTenant: vi.fn() } as any,
    );
    reply = {
      id: 'answer',
      answer: 'Useful partial guidance',
      has_result: true,
      confidence: {
        level: 'low',
        summary: 'Some uncertainty remains.',
        unresolved_needs: ['Confirm the exception.'],
      },
    };
  });

  it('renders confidence while preserving backend-provided mentions', async () => {
    reply.answer += '\n\n<at>API reviewer</at>, could you help?';
    reply.mentions = [{
      text: '<at>API reviewer</at>',
      mentioned: { id: 'reviewer-id', name: 'API reviewer' },
    }];

    await handler.sendReply(
      prompt.timestamp,
      reply,
      prompt,
      history,
      'tenant',
      { show_confidence_label: true },
    );

    const activity = context.sendActivity.mock.calls[0][0];
    expect(activity.text).toContain('**Confidence: Low**\n\nUseful partial guidance');
    expect(activity.text).toContain('Some uncertainty remains.');
    expect(activity.text).toContain('**Unresolved needs**\n- Confirm the exception.');
    expect(activity.entities).toContainEqual({
      type: 'mention',
      text: '<at>API reviewer</at>',
      mentioned: { id: 'reviewer-id', name: 'API reviewer' },
    });
    expect(saveMessage).toHaveBeenCalledWith(
      expect.objectContaining({ activityId: 'reply-id' }),
      expect.anything(),
    );
  });

  it('accepts plain answers without inventing confidence', async () => {
    reply.confidence = undefined;

    await handler.sendReply(
      prompt.timestamp,
      reply,
      prompt,
      history,
      'tenant',
      { show_confidence_label: true },
    );

    expect(context.sendActivity.mock.calls[0][0].text).toBe(reply.answer);
  });

  it('falls back to the ordinary answer for invalid confidence metadata', async () => {
    reply.confidence = {} as any;

    await handler.sendReply(
      prompt.timestamp,
      reply,
      prompt,
      history,
      'tenant',
      { show_confidence_label: true },
    );

    expect(context.sendActivity.mock.calls[0][0].text).toBe(reply.answer);
    expect(logger.warn).toHaveBeenCalledWith(
      'Invalid confidence response; displaying the answer without confidence',
      expect.anything(),
    );
  });
});
