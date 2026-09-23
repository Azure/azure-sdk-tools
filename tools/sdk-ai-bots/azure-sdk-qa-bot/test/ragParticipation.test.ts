import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RAGModel } from '../src/models/RAGModel.js';
import { getRAGReply } from '../src/backend/rag.js';
import type { ChannelBotSettings } from '../src/config/channel.js';
import { ThinkingHandler } from '../src/turn/ThinkingHandler.js';
import { PromptGenerator } from '../src/input/PromptGenerator.js';

vi.mock('../src/backend/auth.js', () => ({ getAccessTokenByManagedIdentity: vi.fn().mockResolvedValue({ token: 'token' }) }));
vi.mock('../src/backend/rag.js', async importOriginal => ({
  ...await importOriginal<typeof import('../src/backend/rag.js')>(),
  getRAGReply: vi.fn(),
}));
vi.mock('../src/config/config.js', () => ({ default: {}, ragApiPaths: { completion: '/completion' } }));
vi.mock('../src/logging/logger.js', () => ({ logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn() } }));
vi.mock('../src/logging/utils.js', () => ({ getTurnContextLogMeta: () => ({}) }));
vi.mock('../src/turn/ThinkingHandler.js', () => ({
  ThinkingHandler: vi.fn(class {
    start = vi.fn().mockResolvedValue(new Date());
    stop = vi.fn().mockResolvedValue(undefined);
  }),
}));
vi.mock('../src/input/PromptGenerator.js', () => ({
  PromptGenerator: vi.fn(class {
    generateCurrentPrompt = vi.fn().mockReturnValue({});
    generateFullPrompt = vi.fn().mockResolvedValue({
      currentQuestion: 'Question', userName: 'User', userID: 'user',
      conversationID: 'channel;messageid=root', additionalInfo: { images: [] },
    });
  }),
}));

describe('approved-request answer flow', () => {
  let model: RAGModel;
  let context: any;
  let settings: ChannelBotSettings;
  const getBotSettings = vi.fn();
  const plainReply = { id: 'answer', answer: 'Answer', has_result: true };
  const assessedReply = {
    ...plainReply, notify_experts: true,
    confidence: { level: 'low' as const, summary: 'Partial guidance', unresolved_needs: ['Human advice'], needs_expert_help: true },
  };
  const run = () => model.completePrompt(context, {} as any, {} as any, {} as any, {} as any);
  const handler = () => vi.mocked(ThinkingHandler).mock.results[0].value;

  beforeEach(() => {
    vi.clearAllMocks();
    settings = { show_confidence_label: false, allow_notify_experts: false, experts: [] };
    getBotSettings.mockReturnValue(settings);
    vi.mocked(getRAGReply).mockResolvedValue(plainReply);
    model = new RAGModel(
      { getConversationMessages: vi.fn().mockResolvedValue([]) } as any,
      { getRagTenant: () => 'tenant', getRagEndpoint: () => 'https://backend', getBotSettings } as any,
      {} as any, {} as any,
    );
    context = {
      activity: { id: 'original', conversation: { id: 'channel;messageid=root' }, from: { aadObjectId: 'entra-user' } },
      sendActivity: vi.fn(),
    };
  });

  it.each([plainReply, assessedReply])('uses manager settings and the same completion path %#', async reply => {
    vi.mocked(getRAGReply).mockResolvedValue(reply);
    await run();
    expect(getBotSettings).toHaveBeenCalledExactlyOnceWith('channel');
    expect(getRAGReply).toHaveBeenCalledWith({
      tenant_id: 'tenant', conversation_id: 'channel;messageid=root', conversation_type: 'teams_channel',
      message: { role: 'user', content: 'Question', user_name: 'User', user_id: 'user' },
      additional_infos: [],
    }, expect.anything(), expect.anything());
    const promptGenerator = vi.mocked(PromptGenerator).mock.results[0].value;
    expect(handler().start.mock.invocationCallOrder[0]).toBeLessThan(promptGenerator.generateCurrentPrompt.mock.invocationCallOrder[0]);
    expect(handler().stop).toHaveBeenCalledExactlyOnceWith(expect.any(Date), reply, expect.anything(), 'tenant', settings);
  });

  it('keeps the settings loaded for the request if configuration changes during generation', async () => {
    vi.mocked(getRAGReply).mockImplementationOnce(async () => {
      getBotSettings.mockReturnValue({ ...settings, show_confidence_label: true });
      return assessedReply;
    });
    await run();
    expect(getBotSettings).toHaveBeenCalledOnce();
    expect(handler().stop).toHaveBeenCalledWith(expect.any(Date), assessedReply, expect.anything(), 'tenant', settings);
  });

  it('preserves propagation of unexpected completion errors', async () => {
    vi.mocked(getRAGReply).mockRejectedValue(new Error('offline'));
    await expect(run()).rejects.toThrow('offline');
    expect(handler().stop).not.toHaveBeenCalled();
  });

  it.each(['empty', 'api-error'])('finishes the existing Thinking activity on generation failure: %s', async failure => {
    if (failure === 'empty') vi.mocked(getRAGReply).mockResolvedValue(undefined);
    else vi.mocked(getRAGReply).mockResolvedValue({ code: 'LLM_SERVICE_FAILURE', category: 'service', message: 'offline' });
    await run();
    expect(handler().stop).toHaveBeenCalledOnce();
    expect(context.sendActivity).not.toHaveBeenCalled();
    if (failure !== 'api-error') {
      expect(handler().stop).toHaveBeenCalledWith(expect.any(Date), expect.objectContaining({ has_result: false }), expect.anything(), 'tenant', settings);
    }
  });
});
