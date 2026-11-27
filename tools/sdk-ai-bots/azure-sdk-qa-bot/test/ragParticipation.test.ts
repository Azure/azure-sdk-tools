import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RAGModel } from '../src/models/RAGModel.js';
import { getRAGReply } from '../src/backend/rag.js';
import type { ChannelBotSettings } from '../src/config/channel.js';
import { ReplyHandler } from '../src/turn/ReplyHandler.js';
import { PromptGenerator } from '../src/input/PromptGenerator.js';

vi.mock('../src/backend/auth.js', () => ({
  getAccessTokenByManagedIdentity: vi.fn().mockResolvedValue({ token: 'token' }),
}));
vi.mock('../src/backend/rag.js', async (importOriginal) => ({
  ...await importOriginal<typeof import('../src/backend/rag.js')>(),
  getRAGReply: vi.fn(),
}));
vi.mock('../src/config/config.js', () => ({
  default: {},
  ragApiPaths: { completion: '/completion' },
}));
vi.mock('../src/logging/logger.js', () => ({
  logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn() },
}));
vi.mock('../src/logging/utils.js', () => ({
  getTurnContextLogMeta: () => ({}),
}));
vi.mock('../src/turn/ReplyHandler.js', () => ({
  ReplyHandler: vi.fn(class {
    sendReply = vi.fn().mockResolvedValue(undefined);
  }),
}));
vi.mock('../src/input/PromptGenerator.js', () => ({
  PromptGenerator: vi.fn(class {
    generateCurrentPrompt = vi.fn().mockReturnValue({});
    generateFullPrompt = vi.fn().mockResolvedValue({
      currentQuestion: 'Question',
      userName: 'User',
      userID: 'user',
      conversationID: 'channel;messageid=root',
      additionalInfo: { images: [] },
    });
  }),
}));

describe('approved-request answer flow', () => {
  let model: RAGModel;
  let context: any;
  let settings: ChannelBotSettings;
  const getBotSettings = vi.fn();
  const getChannelConfig = vi.fn();
  const plainReply = { id: 'answer', answer: 'Answer', has_result: true };
  const assessedReply = {
    ...plainReply,
    confidence: {
      level: 'low' as const,
      summary: 'Partial guidance',
      unresolved_needs: ['Confirm the exception'],
    },
  };
  const run = () => model.completePrompt(context, {} as any, {} as any, {} as any, {} as any);
  const handler = () => vi.mocked(ReplyHandler).mock.results[0].value;

  beforeEach(() => {
    vi.clearAllMocks();
    settings = { show_confidence_label: false };
    getBotSettings.mockReturnValue(settings);
    getChannelConfig.mockReturnValue({
      id: 'channel',
      name: 'API Spec Review',
      tenant: 'tenant',
      endpoint: 'https://backend',
    });
    vi.mocked(getRAGReply).mockResolvedValue(plainReply);
    model = new RAGModel(
      { getConversationMessages: vi.fn().mockResolvedValue([]) } as any,
      { getChannelConfig, getBotSettings } as any,
      {} as any,
      {} as any,
    );
    context = {
      activity: {
        id: 'original',
        conversation: { id: 'channel;messageid=root' },
        channelData: { channel: { name: 'API Spec Review' } },
      },
    };
  });

  it.each([plainReply, assessedReply])('uses one settings snapshot and completion path %#', async (reply) => {
    vi.mocked(getRAGReply).mockResolvedValue(reply);

    await run();

    expect(getBotSettings).toHaveBeenCalledExactlyOnceWith('channel');
    expect(getRAGReply).toHaveBeenCalledWith({
      tenant_id: 'tenant',
      channel_name: 'API Spec Review',
      conversation_id: 'channel;messageid=root',
      conversation_type: 'teams_channel',
      message: {
        role: 'user',
        content: 'Question',
        user_name: 'User',
        user_id: 'user',
      },
      additional_infos: [],
    }, expect.anything(), expect.anything());
    expect(handler().sendReply).toHaveBeenCalledExactlyOnceWith(
      expect.any(Date),
      reply,
      expect.anything(),
      [],
      'tenant',
      settings
    );
  });

  it('keeps the settings loaded before generation when configuration changes', async () => {
    vi.mocked(getRAGReply).mockImplementationOnce(async () => {
      getBotSettings.mockReturnValue({ show_confidence_label: true });
      return assessedReply;
    });

    await run();

    expect(getBotSettings).toHaveBeenCalledOnce();
    expect(handler().sendReply).toHaveBeenCalledWith(
      expect.any(Date),
      assessedReply,
      expect.anything(),
      [],
      'tenant',
      settings
    );
  });

  it('preserves propagation of unexpected completion errors', async () => {
    vi.mocked(getRAGReply).mockRejectedValue(new Error('offline'));

    await expect(run()).rejects.toThrow('offline');

    expect(handler().sendReply).not.toHaveBeenCalled();
  });
});
