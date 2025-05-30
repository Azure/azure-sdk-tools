import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ConversationHandler, ConversationMessage } from '../src/input/ConversationHandler.js';

// Mock the CosmosClient and related classes
vi.mock('@azure/cosmos', () => {
  return {
    CosmosClient: vi.fn().mockImplementation(() => {
      return {
        databases: {
          createIfNotExists: vi.fn().mockResolvedValue({
            database: {
              containers: {
                createIfNotExists: vi.fn().mockResolvedValue({
                  container: {
                    items: {
                      create: vi.fn().mockResolvedValue({
                        resource: { id: 'test-id' },
                      }),
                      query: vi.fn().mockReturnValue({
                        fetchAll: vi.fn().mockResolvedValue({
                          resources: [
                            {
                              conversationId: 'test-conversation',
                              messageId: 'test-message',
                              messageType: 'question',
                              content: 'Hello, world!',
                              imageLinks: [],
                            },
                          ],
                        }),
                      }),
                    },
                  },
                }),
              },
            },
          }),
        },
      };
    }),
  };
});

// Mock config
vi.mock('../src/config/config.js', () => {
  return {
    default: {
      MicrosoftAppId: 'test-bot-id',
      cosmosDbEndpoint: 'https://test-endpoint.documents.azure.com:443/',
      cosmosDbKey: 'test-key',
      cosmosDbDatabaseId: 'test-database',
      cosmosDbContainerId: 'test-container',
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
    handler = new ConversationHandler({ testContext: true });
  });

  it('should initialize correctly', async () => {
    await handler.initialize();
    expect(handler).toBeDefined();
  });

  it('should save a message', async () => {
    await handler.initialize();

    const testMessage: ConversationMessage = {
      conversationId: 'test-conversation',
      activityId: 'test-message',
      text: 'Hello, world!',
      timestamp: new Date(),
    };

    const result = await handler.saveMessage(testMessage);
    expect(result).toBeDefined();
  });

  it('should retrieve conversation messages', async () => {
    await handler.initialize();

    const messages = await handler.getConversationMessages('test-conversation');
    expect(messages).toHaveLength(1);
    expect(messages[0].conversationId).toBe('test-conversation');
    expect(messages[0].activityId).toBe('test-message');
  });

  it('should retrieve a specific message', async () => {
    await handler.initialize();

    const message = await handler.getMessage('test-conversation', 'test-message');
    expect(message).toBeDefined();
    expect(message?.conversationId).toBe('test-conversation');
    expect(message?.activityId).toBe('test-message');
  });
});
