import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PromptGenerator } from '../src/input/PromptGenerator.js';
import { Prompt, ConversationMessage, RAGReply } from '../src/input/ConversationHandler.js';
import { TurnContext } from 'botbuilder';
import { logger } from '../src/logging/logger.js';
import axios from 'axios';
import { getRAGReply, CompletionRequestPayload } from '../src/backend/rag.js';

vi.mock('axios', () => ({
  default: {
    post: vi.fn(),
    isAxiosError: (error: { isAxiosError?: boolean }) => error.isAxiosError === true,
  },
}));
vi.mock('../src/config/config.js', () => ({
  ragApiPaths: { completion: '/agent/chat' },
}));

// Mock logger
vi.mock('../src/logging/logger.js', () => {
  return {
    logger: {
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
    },
  };
});

describe('PromptGenerator', () => {
  let promptGenerator: PromptGenerator;

  beforeEach(() => {
    vi.clearAllMocks();
    promptGenerator = new PromptGenerator();
  });

  describe('RAG inline image payload', () => {
    const image = 'data:image/png;base64,iVBORw0KGgo=';
    const payload: CompletionRequestPayload = {
      tenant_id: 'test',
      message: { role: 'user', content: 'What error is visible?' },
      additional_infos: [{ type: 'image', content: '', link: image }],
    };

    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('sends the original image bytes but omits them from payload logs', async () => {
      vi.mocked(axios.post).mockResolvedValue({
        status: 200,
        data: { id: 'reply', answer: 'The screenshot shows an error.', has_result: true },
      });
      await getRAGReply(payload, { endpoint: 'https://backend.example' }, {});
      expect(axios.post).toHaveBeenCalledWith(
        'https://backend.example/agent/chat', payload, expect.any(Object),
      );
      expect(payload.additional_infos[0].link).toBe(image);
      expect(JSON.stringify(vi.mocked(logger.info).mock.calls)).not.toContain(image);
    });

    it('does not log the Axios request body when an image request fails', async () => {
      vi.mocked(axios.post).mockRejectedValue({
        isAxiosError: true,
        name: 'AxiosError',
        message: 'Request failed',
        response: { status: 500 },
        config: { data: JSON.stringify(payload) },
      });
      expect(await getRAGReply(payload, { endpoint: 'https://backend.example' }, {})).toBeUndefined();
      expect(logger.warn).toHaveBeenCalled();
      expect(JSON.stringify(vi.mocked(logger.warn).mock.calls)).not.toContain(image);
    });
  });

  it('should generate prompt with images', async () => {
    const userQuery = 'How do I use Azure SDK with these resources?';
    const prompt: Prompt = {
      textWithoutMention: userQuery,
      images: ['http://example.com/image1.jpg'],
      userName: 'testUser',
      userID: 'user-123',
      timestamp: new Date('2023-01-01T00:00:00Z'),
      conversationID: 'test-conversation',
    };

    const conversationMessages: ConversationMessage[] = [
      {
        conversationId: 'test-conversation-1',
        activityId: 'message-1',
        text: 'I need help with Azure SDK',
        prompt: {
          textWithoutMention: 'I need help with Azure SDK',
          userName: 'testUser',
          userID: 'user-123',
          timestamp: new Date('2023-01-01T00:00:00Z'),
          conversationID: 'test-conversation-1',
        },
        timestamp: new Date('2023-01-01T00:00:00Z'),
      },
    ];

    const result = await promptGenerator.generateFullPrompt(prompt, conversationMessages, {});

    expect(result.currentQuestion).toBe(userQuery);
    expect(result.userName).toBe('testUser');
    expect(result.userID).toBe('user-123');
    expect(result.conversationID).toBe('test-conversation');
    expect(result.additionalInfo.images).toBeDefined();
    expect(result.additionalInfo.images.length).toBe(1);
  });

  it('should forward connector image bytes without logging them', async () => {
    const dataUri = 'data:image/png;base64,iVBORw0KGgo=';
    const context = {
      activity: {
        type: 'message',
        text: 'What error is visible?',
        attachments: [{ contentType: 'image/*', contentUrl: dataUri }],
        from: { name: 'testUser', id: 'user-123' },
        recipient: { id: 'bot' },
        conversation: { id: 'conversation' },
      },
    } as TurnContext;
    const current = promptGenerator.generateCurrentPrompt(context, {});
    const full = await promptGenerator.generateFullPrompt(current, [], {});

    expect(current.images).toEqual([dataUri]);
    expect(full.additionalInfo.images[0].url.toString()).toBe(dataUri);
    expect(JSON.stringify(vi.mocked(logger.info).mock.calls)).not.toContain(dataUri);
  });
});
