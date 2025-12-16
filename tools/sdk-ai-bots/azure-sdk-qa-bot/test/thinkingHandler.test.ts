import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ThinkingHandler } from '../src/turn/ThinkingHandler.js';
import { ConversationHandler, Prompt } from '../src/input/ConversationHandler.js';
import { CompletionResponsePayload, RagApiError, ErrorCode, ErrorCategory } from '../src/backend/rag.js';
import { TurnContext } from 'botbuilder';

// Mock dependencies
vi.mock('../src/logging/utils.js', () => ({
  getTurnContextLogMeta: vi.fn().mockReturnValue({ meta: 'test' }),
}));

vi.mock('../src/logging/logger.js', () => ({
  logger: {
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock('../src/cards/components/contact.js', () => ({
  createContactCard: vi.fn().mockReturnValue({}),
}));

vi.mock('../src/config/config.js', () => ({
  contactCardVersion: '1.0',
}));

vi.mock('node:timers/promises', () => ({
  setTimeout: vi.fn().mockResolvedValue(undefined),
}));

describe('ThinkingHandler', () => {
  let thinkingHandler: ThinkingHandler;
  let mockContext: Partial<TurnContext>;
  let mockConversationHandler: Partial<ConversationHandler>;

  beforeEach(() => {
    // Mock TurnContext
    mockContext = {
      activity: {
        conversation: { id: 'test-conversation' },
        id: 'test-activity',
      },
      sendActivity: vi.fn().mockResolvedValue({ id: 'test-resource' }),
      updateActivity: vi.fn().mockResolvedValue({ id: 'test-response' }),
    } as any;

    // Mock ConversationHandler
    mockConversationHandler = {
      saveMessage: vi.fn().mockResolvedValue({}),
    };

    thinkingHandler = new ThinkingHandler(mockContext as TurnContext, mockConversationHandler as ConversationHandler);
  });

  describe('generateAnswer', () => {
    it('should return error message for validation error', () => {
      const ragError: RagApiError = {
        code: 'INVALID_REQUEST',
        message: 'Request validation failed',
        category: 'validation',
      };

      // Access private method using type assertion
      const result = (thinkingHandler as any).generateAnswer(ragError);

      expect(result).toBe(
        "🚫Sorry, I'm having some validation issues right now and can't answer your question. Error: Request validation failed."
      );
    });

    it('should return error message for authentication error', () => {
      const ragError: RagApiError = {
        code: 'UNAUTHORIZED',
        message: 'Authentication failed',
        category: 'authentication',
      };

      const result = (thinkingHandler as any).generateAnswer(ragError);

      expect(result).toBe(
        "🚫Sorry, I'm having some authentication issues right now and can't answer your question. Error: Authentication failed."
      );
    });

    it('should return error message for service error', () => {
      const ragError: RagApiError = {
        code: 'LLM_SERVICE_FAILURE' as ErrorCode,
        message: 'LLM service is temporarily unavailable',
        category: 'service' as ErrorCategory,
      };

      const result = (thinkingHandler as any).generateAnswer(ragError);

      expect(result).toBe(
        "🚫Sorry, I'm having some service issues right now and can't answer your question. Please try again later. Error: LLM service is temporarily unavailable."
      );
    });

    it('should return error message for internal error', () => {
      const ragError: RagApiError = {
        code: 'INTERNAL_ERROR' as ErrorCode,
        message: 'An unexpected error occurred',
        category: 'internal' as ErrorCategory,
      };

      const result = (thinkingHandler as any).generateAnswer(ragError);

      expect(result).toBe(
        "🚫Sorry, I'm having some internal issues right now and can't answer your question. Error: An unexpected error occurred."
      );
    });

    it('should return formatted answer with references for successful response', () => {
      const successResponse: CompletionResponsePayload = {
        id: 'test-id',
        answer: 'This is a test answer',
        has_result: true,
        references: [
          {
            title: 'Test Reference',
            source: 'test_source',
            link: 'https://example.com/test',
            content: 'Test content',
          },
        ],
      };

      const result = (thinkingHandler as any).generateAnswer(successResponse);

      expect(result).toContain('This is a test answer');
      expect(result).toContain('**References**');
      expect(result).toContain('[Test Reference | Test Source](https://example.com/test)');
    });

    it('should return answer without references when no references provided', () => {
      const successResponse: CompletionResponsePayload = {
        id: 'test-id',
        answer: 'This is a test answer without references',
        has_result: true,
        references: [],
      };

      const result = (thinkingHandler as any).generateAnswer(successResponse);

      expect(result).toBe('This is a test answer without references\n\n> **NOTE:** If you have follow-up questions after my response, please @Azure SDK Q&A Bot to continue the conversation.');
      expect(result).not.toContain('**References**');
    });
  });

  describe('convertPayloadToReply', () => {
    it('should convert validation error to RAGReply with error message', () => {
      const ragError: RagApiError = {
        code: 'MISSING_MESSAGE' as ErrorCode,
        message: 'Message content is required',
        category: 'validation' as ErrorCategory,
      };

      const result = (thinkingHandler as any).convertPayloadToReply(ragError);

      expect(result).toEqual({
        answer:
          "🚫Sorry, I'm having some validation issues right now and can't answer your question. Error: Message content is required.",
        has_result: false,
        references: [],
      });
    });

    it('should convert authorization error to RAGReply with error message', () => {
      const ragError: RagApiError = {
        code: 'INVALID_TENANT_ID' as ErrorCode,
        message: 'Tenant ID is invalid',
        category: 'authorization' as ErrorCategory,
      };

      const result = (thinkingHandler as any).convertPayloadToReply(ragError);

      expect(result).toEqual({
        answer:
          "🚫Sorry, I'm having some authorization issues right now and can't answer your question. Error: Tenant ID is invalid.",
        has_result: false,
        references: [],
      });
    });

    it('should convert dependency error to RAGReply with error message', () => {
      const ragError: RagApiError = {
        code: 'SEARCH_FAILURE' as ErrorCode,
        message: 'Search service is not responding',
        category: 'dependency' as ErrorCategory,
      };

      const result = (thinkingHandler as any).convertPayloadToReply(ragError);

      expect(result).toEqual({
        answer:
          "🚫Sorry, I'm having some dependency issues right now and can't answer your question. Please try again later. Error: Search service is not responding.",
        has_result: false,
        references: [],
      });
    });

    it('should convert rate limit error to RAGReply with error message', () => {
      const ragError: RagApiError = {
        code: 'SERVICE_INIT_FAILURE' as ErrorCode,
        message: 'Service initialization failed',
        category: 'rate_limit' as ErrorCategory,
      };

      const result = (thinkingHandler as any).convertPayloadToReply(ragError);

      expect(result).toEqual({
        answer:
          "🚫Sorry, I'm having some rate_limit issues right now and can't answer your question. Error: Service initialization failed.",
        has_result: false,
        references: [],
      });
    });

    it('should convert successful response to RAGReply', () => {
      const successResponse: CompletionResponsePayload = {
        id: 'test-id',
        answer: 'This is a successful response',
        has_result: true,
        references: [
          {
            title: 'Reference 1',
            source: 'source_1',
            link: 'https://example.com/ref1',
            content: 'Reference content 1',
          },
          {
            title: 'Reference 2',
            source: 'source_2',
            link: 'https://example.com/ref2',
            content: 'Reference content 2',
          },
        ],
      };

      const result = (thinkingHandler as any).convertPayloadToReply(successResponse);

      expect(result).toEqual({
        answer: 'This is a successful response',
        has_result: true,
        references: [
          {
            title: 'Reference 1',
            source: 'source_1',
            link: 'https://example.com/ref1',
            content: 'Reference content 1',
          },
          {
            title: 'Reference 2',
            source: 'source_2',
            link: 'https://example.com/ref2',
            content: 'Reference content 2',
          },
        ],
      });
    });

    it('should handle successful response with no references', () => {
      const successResponse: CompletionResponsePayload = {
        id: 'test-id',
        answer: 'This is a response without references',
        has_result: true,
        references: [],
      };

      const result = (thinkingHandler as any).convertPayloadToReply(successResponse);

      expect(result).toEqual({
        answer: 'This is a response without references',
        has_result: true,
        references: [],
      });
    });

    it('should handle successful response with undefined references', () => {
      const successResponse: CompletionResponsePayload = {
        id: 'test-id',
        answer: 'This is a response with undefined references',
        has_result: true,
      };

      const result = (thinkingHandler as any).convertPayloadToReply(successResponse);

      expect(result).toEqual({
        answer: 'This is a response with undefined references',
        has_result: true,
        references: [],
      });
    });
  });

  describe('Integration tests for error scenarios', () => {
    it('should handle complete flow with validation error', async () => {
      const ragError: RagApiError = {
        code: 'EMPTY_CONTENT' as ErrorCode,
        message: 'Message content cannot be empty',
        category: 'validation' as ErrorCategory,
      };

      const mockPrompt: Prompt = {
        textWithoutMention: 'Test prompt',
        userName: 'TestUser',
        timestamp: new Date(),
      };

      // Test the stop method with error
      await thinkingHandler.stop(new Date(), ragError, mockPrompt);

      // Verify that updateActivity was called with error message
      expect(mockContext.updateActivity).toHaveBeenCalledWith({
        type: 'message',
        id: undefined, // resourceId is undefined in this test
        text: "🚫Sorry, I'm having some validation issues right now and can't answer your question. Error: Message content cannot be empty.",
        conversation: mockContext.activity?.conversation,
      });
    });

    it('should handle complete flow with service error', async () => {
      const ragError: RagApiError = {
        code: 'LLM_SERVICE_FAILURE' as ErrorCode,
        message: 'LLM model is currently overloaded',
        category: 'service' as ErrorCategory,
      };

      const mockPrompt: Prompt = {
        textWithoutMention: 'Another test prompt',
        userName: 'TestUser',
        timestamp: new Date(),
      };

      // Test the stop method with error
      await thinkingHandler.stop(new Date(), ragError, mockPrompt);

      // Verify that updateActivity was called with service error message
      expect(mockContext.updateActivity).toHaveBeenCalledWith({
        type: 'message',
        id: undefined,
        text: "🚫Sorry, I'm having some service issues right now and can't answer your question. Please try again later. Error: LLM model is currently overloaded.",
        conversation: mockContext.activity?.conversation,
      });
    });
  });
});
