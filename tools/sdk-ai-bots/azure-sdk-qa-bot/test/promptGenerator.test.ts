import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PromptGenerator } from '../src/input/PromptGenerator.js';
import { Prompt, ConversationMessage, RAGReply } from '../src/input/ConversationHandler.js';

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
    promptGenerator = new PromptGenerator();
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
});
