import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ConversationHandler, ConversationMessage, Prompt, RAGReply } from '../src/input/ConversationHandler.js';
import { CompletionResponsePayload, Reference } from '../src/backend/rag.js';

const testTimestamp = new Date('2023-10-01T12:00:00Z');

const testPrompt: Prompt = {
  textWithoutMention: 'Hello, world!',
  links: ['http://example.com/aaa/', 'http://example.com/bbb/'],
  images: ['http://example.com/image1.jpg', 'http://example.com/image2.jpg'],
  userName: 'test-user',
  timestamp: testTimestamp,
};

const references: Reference[] = [
  {
    title: 'Test Source 1',
    source: 'http://example.com/source1',
    link: 'http://example.com/source1',
    content: 'Content from source 1',
  },
  {
    title: 'Test Source 2',
    source: 'http://example.com/source2',
    link: 'http://example.com/source2',
    content: 'Content from source 2',
  },
];

const testReply: RAGReply = {
  answer: 'This is a test reply',
  references: references,
  has_result: true,
};

const testMessage: ConversationMessage = {
  conversationId: 'test-conversation',
  activityId: 'test-message',
  text: 'Hello, world!',
  timestamp: testTimestamp,
  prompt: testPrompt,
  reply: testReply,
};

// Mock Azure Table Storage client and related classes
vi.mock('@azure/data-tables', () => {
  return {
    TableClient: vi.fn().mockImplementation(() => {
      return {
        createEntity: vi.fn().mockResolvedValue({
          partitionKey: 'test-conversation',
          rowKey: 'test-message',
          text: 'Hello, world!',
          prompt: JSON.stringify(testPrompt),
          reply: JSON.stringify(testReply),
          timestamp: testTimestamp,
        }),
        getEntity: vi.fn().mockResolvedValue({
          partitionKey: 'test-conversation',
          rowKey: 'test-message',
          text: 'Hello, world!',
          prompt: JSON.stringify(testPrompt),
          reply: JSON.stringify(testReply),
          timestamp: testTimestamp,
        }),
        listEntities: vi.fn().mockReturnValue({
          async *[Symbol.asyncIterator]() {
            yield {
              partitionKey: 'test-conversation',
              rowKey: 'test-message',
              text: 'Hello, world!',
              prompt: JSON.stringify(testPrompt),
              reply: JSON.stringify(testReply),
              timestamp: testTimestamp,
            };
          },
        }),
      };
    }),
    TableServiceClient: vi.fn().mockImplementation(() => {
      return {
        createTable: vi.fn().mockResolvedValue({}),
      };
    }),
    odata: vi.fn().mockImplementation((strings, ...values) => {
      return `PartitionKey eq '${values[0]}'`;
    }),
  };
});

// Mock Azure Identity
vi.mock('@azure/identity', () => {
  return {
    DefaultAzureCredential: vi.fn().mockImplementation(() => ({})),
    ManagedIdentityCredential: vi.fn().mockImplementation(() => ({})),
  };
});

// Mock config
vi.mock('../src/config/config.js', () => {
  return {
    default: {
      MicrosoftAppId: 'test-bot-id',
      azureStorageUrl: 'https://test-storage.table.core.windows.net/',
      azureTableNameForConversation: 'conversations',
    },
  };
});

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

describe('ConversationHandler', () => {
  let handler: ConversationHandler;

  beforeEach(() => {
    handler = new ConversationHandler();
  });

  it('should initialize correctly', async () => {
    await handler.initialize();
    expect(handler).toBeDefined();
  });

  it('should save a message', async () => {
    await handler.initialize();
    const result = await handler.saveMessage(testMessage);
    expect(result).toBeDefined();
    expect(result.partitionKey).toBe('test-conversation');
    expect(result.rowKey).toBe('test-message');
    expect(result.text).toBe('Hello, world!');
    expect(result.prompt).toEqual(JSON.stringify(testPrompt));
    expect(result.reply).toEqual(JSON.stringify(testReply));
  });

  it('should retrieve conversation messages', async () => {
    await handler.initialize();

    const messages = await handler.getConversationMessages('test-conversation');
    expect(messages).toHaveLength(1);
    expect(messages[0].conversationId).toBe('test-conversation');
    expect(messages[0].activityId).toBe('test-message');
    expect(messages[0].text).toBe('Hello, world!');
    expect(JSON.stringify(messages[0].prompt)).toEqual(JSON.stringify(testPrompt));
    expect(JSON.stringify(messages[0].reply)).toEqual(JSON.stringify(testReply));
  });
});
