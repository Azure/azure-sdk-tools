import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  ConversationHandler,
  ConversationMessage,
  Prompt,
  RAGReply,
  ContactCard,
} from '../src/input/ConversationHandler.js';
import { Reference } from '../src/backend/rag.js';

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

const testContactCard: ContactCard = {
  version: '1.0',
};

const testMessage: ConversationMessage = {
  conversationId: 'test-conversation',
  activityId: 'test-message',
  text: 'Hello, world!',
  timestamp: testTimestamp,
  prompt: testPrompt,
  reply: testReply,
};

const testMessageWithContactCard: ConversationMessage = {
  conversationId: 'test-conversation-card',
  activityId: 'test-message-card',
  text: 'Contact card message',
  timestamp: testTimestamp,
  contactCard: testContactCard,
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
          contactCard: undefined,
          timestamp: testTimestamp,
        }),
        getEntity: vi.fn().mockResolvedValue({
          partitionKey: 'test-conversation',
          rowKey: 'test-message',
          text: 'Hello, world!',
          prompt: JSON.stringify(testPrompt),
          reply: JSON.stringify(testReply),
          contactCard: undefined,
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
              contactCard: undefined,
              timestamp: testTimestamp,
            };
            yield {
              partitionKey: 'test-conversation-card',
              rowKey: 'test-message-card',
              text: 'Contact card message',
              prompt: undefined,
              reply: undefined,
              contactCard: JSON.stringify(testContactCard),
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
    const result = await handler.saveMessage(testMessage, {});
    expect(result).toBeDefined();
    expect(result.partitionKey).toBe('test-conversation');
    expect(result.rowKey).toBe('test-message');
    expect(result.text).toBe('Hello, world!');
    expect(result.prompt).toEqual(JSON.stringify(testPrompt));
    expect(result.reply).toEqual(JSON.stringify(testReply));
    expect(result.contactCard).toBeUndefined();
  });

  it('should save a message with contact card', async () => {
    await handler.initialize();
    const result = await handler.saveMessage(testMessageWithContactCard, {});
    expect(result).toBeDefined();
    expect(result.partitionKey).toBe('test-conversation-card');
    expect(result.rowKey).toBe('test-message-card');
    expect(result.text).toBe('Contact card message');
    expect(result.prompt).toBeUndefined();
    expect(result.reply).toBeUndefined();
    expect(result.contactCard).toEqual(JSON.stringify(testContactCard));
  });

  it('should retrieve conversation messages', async () => {
    await handler.initialize();

    const messages = await handler.getConversationMessages('test-conversation', {});
    expect(messages).toHaveLength(2);

    // First message with prompt and reply
    const messageWithPrompt = messages.find((m) => m.activityId === 'test-message');
    expect(messageWithPrompt.conversationId).toBe('test-conversation');
    expect(messageWithPrompt.activityId).toBe('test-message');
    expect(messageWithPrompt.text).toBe('Hello, world!');
    expect(JSON.stringify(messageWithPrompt.prompt)).toEqual(JSON.stringify(testPrompt));
    expect(JSON.stringify(messageWithPrompt.reply)).toEqual(JSON.stringify(testReply));
    expect(messageWithPrompt.contactCard).toBeUndefined();

    // Second message with contact card
    const messageWithContactCard = messages.find((m) => m.activityId === 'test-message-card');
    expect(messageWithContactCard.conversationId).toBe('test-conversation-card');
    expect(messageWithContactCard.activityId).toBe('test-message-card');
    expect(messageWithContactCard.text).toBe('Contact card message');
    expect(messageWithContactCard.prompt).toBeUndefined();
    expect(messageWithContactCard.reply).toBeUndefined();
    expect(JSON.stringify(messageWithContactCard.contactCard)).toEqual(JSON.stringify(testContactCard));
  });

  it('should handle message serialization/deserialization with all fields', async () => {
    await handler.initialize();

    // Create a message with all possible fields
    const fullMessage: ConversationMessage = {
      conversationId: 'full-conversation',
      activityId: 'full-message',
      text: 'Full message with all fields',
      timestamp: testTimestamp,
      prompt: testPrompt,
      reply: testReply,
      contactCard: testContactCard,
    };

    // Save and verify the entity was created correctly
    const savedEntity = await handler.saveMessage(fullMessage, {});
    expect(savedEntity).toBeDefined();
    expect(savedEntity.prompt).toEqual(
      JSON.stringify({
        ...testPrompt,
        timestamp: testPrompt.timestamp.toISOString(),
      })
    );
    expect(savedEntity.reply).toEqual(JSON.stringify(testReply));
    expect(savedEntity.contactCard).toEqual(JSON.stringify(testContactCard));
  });

  it('should handle empty/undefined contact card correctly', async () => {
    await handler.initialize();

    const messageWithoutContactCard: ConversationMessage = {
      conversationId: 'no-card-conversation',
      activityId: 'no-card-message',
      text: 'Message without contact card',
      timestamp: testTimestamp,
    };

    const savedEntity = await handler.saveMessage(messageWithoutContactCard, {});
    expect(savedEntity).toBeDefined();
    expect(savedEntity.contactCard).toBeUndefined();
  });
});
