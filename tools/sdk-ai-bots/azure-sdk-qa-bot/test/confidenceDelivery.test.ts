import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ThinkingHandler } from '../src/turn/ThinkingHandler.js';
import type { ChannelBotSettings } from '../src/config/channel.js';
import type { CompletionResponsePayload } from '../src/backend/rag.js';
import { logger } from '../src/logging/logger.js';
import { sendActivityWithRetry, updateActivityWithRetry } from '../src/activityUtils.js';
import { KnownTenants } from '../src/config/tenant.js';

vi.mock('../src/activityUtils.js', () => ({
  sendActivityWithRetry: vi.fn(),
  updateActivityWithRetry: vi.fn(async (context, activity) => context.updateActivity(activity)),
}));
vi.mock('../src/config/config.js', () => ({ contactCardVersion: '1.0', ragApiPaths: {} }));
vi.mock('../src/logging/utils.js', () => ({ getTurnContextLogMeta: () => ({}) }));
vi.mock('../src/logging/logger.js', () => ({ logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn() } }));
vi.mock('../src/cards/components/contact.js', () => ({ createContactCard: vi.fn() }));

describe('confidence and mention delivery', () => {
  let context: any;
  let handler: ThinkingHandler;
  let saveMessage: ReturnType<typeof vi.fn>;
  let reply: CompletionResponsePayload;
  let settings: ChannelBotSettings;
  const prompt: any = { timestamp: new Date(), textWithoutMention: 'question' };
  const finish = () => handler.stop(prompt.timestamp, reply, prompt, 'tenant', settings);

  beforeEach(() => {
    vi.clearAllMocks();
    context = {
      activity: { id: 'original', conversation: { id: 'channel;messageid=root' } },
      sendActivity: vi.fn().mockResolvedValue({ id: 'teams-id' }),
      updateActivity: vi.fn().mockResolvedValue({ id: 'thinking-id' }),
      deleteActivity: vi.fn().mockResolvedValue(undefined),
    };
    saveMessage = vi.fn().mockResolvedValue(undefined);
    handler = new ThinkingHandler(context, { saveMessage } as any, {
      getTenant: vi.fn().mockReturnValue({ channel_name: 'TypeSpec', channel_link: 'https://example.com/channel' }),
    } as any);
    (handler as any).resourceId = 'thinking-id';
    vi.spyOn(handler, 'safeCancelTimer').mockResolvedValue(undefined);
    reply = {
      id: 'answer', answer: 'Useful partial guidance', has_result: true, notify_experts: true,
      confidence: { level: 'low', summary: 'Cannot confirm the exception.', unresolved_needs: ['Approval required'], needs_expert_help: true },
    };
    settings = { show_confidence_label: true, allow_notify_experts: true, experts: [{ id: 'entra-id', name: 'A & B' }] };
  });

  it('updates Thinking with mention text and entities, preserving its Teams history ID', async () => {
    reply.trace_id = 'trace';
    reply.route_tenant = KnownTenants.TypeSpec;
    reply.references = [{ title: 'Guide', source: 'docs', link: 'https://example.com/guide', content: 'Guide' }];
    await finish();
    const activity = context.updateActivity.mock.calls[0][0];
    expect(activity.id).toBe('thinking-id');
    expect(activity.text).toContain('**Confidence: Low**\n\nUseful partial guidance');
    expect(activity.text).toContain('Cannot confirm the exception.');
    expect(activity.text).toContain('Approval required');
    expect(activity.text).toContain('[Guide | Docs](https://example.com/guide)');
    expect(activity.text).toContain('[TypeSpec](https://example.com/channel)');
    expect(activity.text).toContain('Azure TypeSpec Author skill');
    expect(activity.text).toContain(
      '<at>A &amp; B</at>, could you help with the unresolved parts?\n\n---\n\n',
    );
    expect(activity.entities[0].usageInfo.description).toBe('Trace ID: trace');
    expect(activity.entities).toContainEqual({
      type: 'mention', mentioned: { id: 'entra-id', name: 'A & B' }, text: '<at>A &amp; B</at>',
    });
    expect(activity.text).toContain('<at>A &amp; B</at>');
    expect(handler.safeCancelTimer).toHaveBeenCalledOnce();
    expect(vi.mocked(handler.safeCancelTimer).mock.invocationCallOrder[0]).toBeLessThan(context.updateActivity.mock.invocationCallOrder[0]);
    expect(context.deleteActivity).not.toHaveBeenCalled();
    expect(context.sendActivity).not.toHaveBeenCalled();
    expect(sendActivityWithRetry).not.toHaveBeenCalled();
    expect(updateActivityWithRetry).toHaveBeenCalledOnce();
    expect(context.updateActivity).toHaveBeenCalledOnce();
    expect(saveMessage).toHaveBeenCalledWith(expect.objectContaining({ activityId: 'original', prompt }), expect.anything());
    expect(saveMessage).toHaveBeenCalledWith(expect.objectContaining({ activityId: 'thinking-id' }), expect.anything());
  });

  it.each([
    { permission: false, allowNotify: true, label: true },
    { permission: undefined, allowNotify: true, label: false },
    { permission: true, allowNotify: false, label: false },
  ])('updates Thinking without a new activity when mentions are not authorized %#', async ({ permission, allowNotify, label }) => {
    reply.notify_experts = permission;
    settings.allow_notify_experts = allowNotify;
    settings.show_confidence_label = label;
    await finish();
    const activity = context.updateActivity.mock.calls[0][0];
    expect(activity.id).toBe('thinking-id');
    expect(activity.text.includes('Confidence:')).toBe(label);
    expect(activity.text).toContain('Approval required');
    expect(activity.text).not.toContain('<at>');
    expect(updateActivityWithRetry).toHaveBeenCalledOnce();
    expect(context.sendActivity).not.toHaveBeenCalled();
    expect(context.deleteActivity).not.toHaveBeenCalled();
    expect(saveMessage).toHaveBeenCalledWith(expect.objectContaining({ activityId: 'thinking-id' }), expect.anything());
  });

  it.each([undefined, null])('accepts plain answers without inventing confidence or mentions: %s', async confidence => {
    reply.confidence = confidence;
    reply.notify_experts = false;
    await finish();
    expect(context.updateActivity.mock.calls[0][0].text).toBe(reply.answer);
    expect(logger.error).not.toHaveBeenCalled();
  });

  it('does not recompute backend notification eligibility', async () => {
    reply.confidence = { level: 'high', summary: 'Guidance confidence', unresolved_needs: [], needs_expert_help: false };
    await finish();
    expect(context.updateActivity.mock.calls[0][0].text).toContain('<at>A &amp; B</at>');
  });

  it('keeps error replies free of footers and mentions even when optional features are enabled', async () => {
    await handler.stop(prompt.timestamp, {
      code: 'LLM_SERVICE_FAILURE', category: 'service', message: 'offline',
    }, prompt, KnownTenants.TypeSpec, settings);
    const activity = context.updateActivity.mock.calls[0][0];
    expect(activity.text).toContain('Error: offline');
    expect(activity.text).not.toContain('Azure TypeSpec Author skill');
    expect(activity.text).not.toContain('Confidence:');
    expect(activity.entities).toHaveLength(1);
    expect(context.sendActivity).not.toHaveBeenCalled();
  });

  it.each([{ confidence: {} }, { notify_experts: 'true' }])('falls back to the ordinary answer for invalid assessment metadata %#', async invalid => {
    reply = { ...reply, ...invalid } as CompletionResponsePayload;
    reply.route_tenant = KnownTenants.TypeSpec;
    reply.references = [{ title: 'Guide', source: 'docs', link: 'https://example.com/guide', content: 'Guide' }];
    await finish();
    const activity = context.updateActivity.mock.calls[0][0];
    expect(activity.text).toContain(reply.answer);
    expect(activity.text).toContain('[Guide | Docs](https://example.com/guide)');
    expect(activity.text).toContain('Azure TypeSpec Author skill');
    expect(activity.text).not.toMatch(/Confidence:|Cannot confirm|Unresolved needs|<at>/);
    expect(logger.warn).toHaveBeenCalledWith(
      'Invalid confidence response; displaying the answer without confidence or mentions', expect.anything(),
    );
    expect(logger.error).not.toHaveBeenCalled();
    expect(context.deleteActivity).not.toHaveBeenCalled();
    expect(context.sendActivity).not.toHaveBeenCalled();
    expect(saveMessage).toHaveBeenCalledWith(expect.objectContaining({ activityId: 'thinking-id' }), expect.anything());
  });

  it('logs malformed references without updating Thinking', async () => {
    reply.references = [null];
    await finish();
    expect(context.deleteActivity).not.toHaveBeenCalled();
    expect(context.sendActivity).not.toHaveBeenCalled();
    expect(context.updateActivity).not.toHaveBeenCalled();
    expect(logger.error).toHaveBeenCalled();
  });

  it('delivers the answer without mentions when no recipients are configured', async () => {
    settings.experts = [];
    reply.route_tenant = KnownTenants.TypeSpec;
    await finish();
    expect(context.updateActivity.mock.calls[0][0].text).toContain(reply.answer);
    expect(context.updateActivity.mock.calls[0][0].text).toContain('Azure TypeSpec Author skill');
    expect(context.deleteActivity).not.toHaveBeenCalled();
    expect(context.sendActivity).not.toHaveBeenCalled();
    expect(logger.warn).toHaveBeenCalledWith(
      'Expert notification authorized without recipients; displaying the answer without mentions', expect.anything(),
    );
    expect(logger.error).not.toHaveBeenCalled();
  });

  it.each(['timeout', 'no-id'])('does not send a new activity or save history after a failed mention update: %s', async failure => {
    if (failure === 'timeout') context.updateActivity.mockRejectedValue(new Error('network'));
    else context.updateActivity.mockResolvedValue({});
    await finish();
    expect(context.sendActivity).not.toHaveBeenCalled();
    expect(context.deleteActivity).not.toHaveBeenCalled();
    expect(updateActivityWithRetry).toHaveBeenCalledOnce();
    expect(context.updateActivity).toHaveBeenCalledOnce();
    expect(sendActivityWithRetry).not.toHaveBeenCalled();
    expect(saveMessage).not.toHaveBeenCalled();
    expect(logger.error).toHaveBeenCalled();
  });

  it.each([false, true])('logs history failures without posting another reply, mentions=%s', async notify => {
    reply.notify_experts = notify;
    saveMessage.mockRejectedValue(new Error('storage failure'));
    await finish();
    expect(context.sendActivity).not.toHaveBeenCalled();
    expect(context.updateActivity).toHaveBeenCalledOnce();
    expect(logger.error).toHaveBeenCalled();
  });

  it.each([false, true])('continues delivery when Thinking cannot stop, mentions=%s', async notify => {
    reply.notify_experts = notify;
    vi.mocked(handler.safeCancelTimer).mockRejectedValue(new Error('busy'));
    await finish();
    expect(logger.warn).toHaveBeenCalledWith(
      'Failed to stop Thinking timer; proceeding with reply delivery', expect.anything(),
    );
    expect(context.deleteActivity).not.toHaveBeenCalled();
    expect(context.sendActivity).not.toHaveBeenCalled();
    expect(context.updateActivity).toHaveBeenCalledOnce();
    expect(saveMessage).toHaveBeenCalledWith(
      expect.objectContaining({ activityId: 'thinking-id' }), expect.anything(),
    );
  });
});
