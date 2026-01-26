import { describe, it, expect, beforeAll, afterAll, vi } from 'vitest';

// Mock config to avoid validation errors in test environment
vi.mock('../../src/config/config.js', () => ({
  default: {
    azureStorageUrl: process.env.AZURE_STORAGE_URL,
    azureTableNameForConversation: process.env.AZURE_TABLE_NAME_FOR_CONVERSATION,
    azureBlobStorageUrl: 'https://test.blob.core.windows.net',
    blobContainerName: 'bot-configs',
    channelConfigBlobName: 'channel.yaml',
    tenantConfigBlobName: 'tenant.yaml',
    fallbackRagEndpoint: 'https://test.azurewebsites.net',
    fallbackRagTenant: 'test_tenant',
  },
}));

import { ConversationHandler, ConversationMessage } from '../../src/input/ConversationHandler.js';
import { logger } from '../../src/logging/logger.js';

/**
 * This test requires a valid Azure Table Storage connection.
 * Make sure the following environment variables are set before running this test:
 * - AZURE_STORAGE_URL - The URL of the Azure Table Storage account
 * - AZURE_TABLE_NAME - The name of the table to use
 *
 * If environment variables are not set, this test will be skipped
 */
describe('ConversationHandler E2E', () => {
  // Check if necessary environment variables are set
  const hasTableStorageConfig = !!process.env.AZURE_STORAGE_URL;

  let handler: ConversationHandler;
  let testConversationId: string;
  let testActivityId: string;

  // Set up environment before all tests
  beforeAll(async () => {
    if (!hasTableStorageConfig) {
      console.log('Skipping ConversationHandler E2E tests: Missing AZURE_STORAGE_URL environment variable');
      return;
    }

    // Create a unique conversation ID for testing
    testConversationId = `test-conversation-${Date.now()}`;
    testActivityId = `test-message-${Date.now()}`;

    // Create handler instance and initialize
    handler = new ConversationHandler();
    await handler.initialize();
  }, 30000); // Set 30 seconds timeout as initialization may take time

  // Clean up after all tests
  afterAll(async () => {
    if (!hasTableStorageConfig) return; // You can add cleanup code here, such as deleting data created during the test
    // However, in this example, we are keeping the test data for manual inspection
    logger.info('E2E tests completed, test conversation ID:', { conversationId: testConversationId });
  });

  it('should save messages to Azure Table Storage', async () => {
    if (!hasTableStorageConfig) {
      return;
    }

    const testMessage: ConversationMessage = {
      conversationId: testConversationId,
      activityId: testActivityId,
      text: 'This is an end-to-end test message',
      timestamp: new Date(),
    };

    const savedMessage = await handler.saveMessage(testMessage, {});

    // Verify the message was saved
    expect(savedMessage).toBeDefined();
    expect(savedMessage.conversationId).toBe(testConversationId);
    expect(savedMessage.activityId).toBe(testActivityId);
    expect(savedMessage.text).toBe('This is an end-to-end test message');
  }, 10000); // 10 seconds timeout

  it('should retrieve all messages in a conversation', async () => {
    if (!hasTableStorageConfig) {
      return;
    }

    // Create a second message
    const secondActivityId = `test-message-2-${Date.now()}`;
    const secondMessage: ConversationMessage = {
      conversationId: testConversationId,
      activityId: secondActivityId,
      text: 'This is the second test message',
      timestamp: new Date(),
    };

    // Save the second message
    await handler.saveMessage(secondMessage, {});

    // Retrieve all messages in the conversation
    const messages = await handler.getConversationMessages(testConversationId, {});

    // Verify the conversation has at least two messages
    expect(messages.length).toBeGreaterThanOrEqual(2);

    // Verify both test messages are in the result set
    const foundFirstMessage = messages.some((msg) => msg.activityId === testActivityId);
    const foundSecondMessage = messages.some((msg) => msg.activityId === secondActivityId);

    expect(foundFirstMessage).toBe(true);
    expect(foundSecondMessage).toBe(true);
  }, 10000); // 10 seconds timeout
});
