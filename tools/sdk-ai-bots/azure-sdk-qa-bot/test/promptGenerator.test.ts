import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PromptGenerator } from '../src/input/PromptGeneratorV2.js';
import { Prompt, ConversationMessage, RAGReply } from '../src/input/ConversationHandler.js';

// Mock ImageTextExtractor
vi.mock('../src/input/ImageContentExtractor.js', () => {
  return {
    ImageTextExtractor: vi.fn().mockImplementation(() => {
      return {
        extract: vi.fn().mockResolvedValue([
          {
            id: 'image-0',
            text: 'This is extracted text from the first image',
            url: 'http://example.com/image1.jpg',
          },
          {
            id: 'image-1',
            text: 'This is extracted text from the second image',
            url: 'http://example.com/image2.jpg',
          },
        ]),
      };
    }),
  };
});

// Mock LinkContentExtractor
vi.mock('../src/input/LinkContentExtractor.js', () => {
  return {
    LinkContentExtractor: vi.fn().mockImplementation(() => {
      return {
        extract: vi.fn().mockResolvedValue([
          {
            id: 'link-0',
            text: 'This is the content extracted from the first link about Azure documentation',
            url: 'http://example.com/azure-docs',
          },
          {
            id: 'link-1',
            text: 'This is the content extracted from the second link about API reference',
            url: 'http://example.com/api-reference',
          },
          {
            id: 'link-2',
            text: 'This is the content extracted from the second link about SDK reference',
            url: 'http://example.com/azure-sdk-ref',
          },
        ]),
      };
    }),
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

describe('PromptGenerator', () => {
  let promptGenerator: PromptGenerator;

  beforeEach(() => {
    promptGenerator = new PromptGenerator();
  });

  it('should generate prompt with additional contents', async () => {
    const userQuery = 'How do I use Azure SDK with these resources?';
    const prompt: Prompt = {
      textWithoutMention: userQuery,
      links: ['http://example.com/azure-docs', 'http://example.com/api-reference'],
      images: [],
      userName: 'testUser',
      timestamp: new Date('2023-01-01T00:00:00Z'),
    };

    const mockReply: RAGReply = {
      answer: 'I can help you with Azure SDK. What specific aspect are you interested in?',
      has_result: true,
      references: [
        {
          title: 'Azure SDK Reference',
          source: 'Azure Docs',
          link: 'http://example.com/azure-sdk-ref',
          content: 'Azure SDK documentation content',
        },
        // duplicate case
        {
          title: 'Azure SDK Reference',
          source: 'Azure Docs',
          link: 'http://example.com/azure-sdk-ref',
          content: 'Azure SDK documentation content',
        },
      ],
    };

    const conversationMessages: ConversationMessage[] = [
      {
        conversationId: 'test-conversation-1',
        activityId: 'message-1',
        text: 'I need help with Azure SDK',
        prompt: {
          textWithoutMention: 'I need help with Azure SDK',
          userName: 'testUser',
          timestamp: new Date('2023-01-01T00:00:00Z'),
        },
        timestamp: new Date('2023-01-01T00:00:00Z'),
      },
      {
        conversationId: 'test-conversation-1',
        activityId: 'message-2',
        prompt: {
          textWithoutMention: 'I need help with Azure SDK',
          userName: 'testUser',
          timestamp: new Date('2022-01-01T00:00:00Z'),
        },
        reply: mockReply,
        timestamp: new Date('2022-01-01T00:01:00Z'),
      },
    ];

    const result = await promptGenerator.generateFullPrompt(prompt, conversationMessages, {});

    // Since generateFullPrompt returns MessageWithRemoteContent, we need to check the currentQuestion property
    expect(result.currentQuestion).toContain(userQuery);
    expect(result.currentQuestion).toContain('How do I use Azure SDK with these resources?');

    // Check that conversations array is defined and contains expected content
    expect(result.conversations).toBeDefined();
    expect(result.conversations.length).toBe(2);

    // Check the first conversation in the array
    expect(result.conversations[0].question).toContain('I need help with Azure SDK');
    expect(result.conversations[1].answer).toContain('I can help you with Azure SDK');

    // Verify that the additional info contains extracted content from links
    expect(result.additionalInfo.links).toBeDefined();
    expect(result.additionalInfo.links.length).toBe(3);

    // Verify that the extracted content includes expected URLs
    const linkUrls = result.additionalInfo.links.map((content) => content.url.toString());
    expect(linkUrls).toContain('http://example.com/azure-docs');
    expect(linkUrls).toContain('http://example.com/api-reference');

    // Verify that the extracted content includes expected text content
    const linkTexts = result.additionalInfo.links.map((content) => content.text);
    expect(linkTexts).toContain('This is the content extracted from the first link about Azure documentation');
    expect(linkTexts).toContain('This is the content extracted from the second link about API reference');

    // Verify specific link content by URL
    const azureDocsLink = result.additionalInfo.links.find(
      (content) => content.url.toString() === 'http://example.com/azure-docs'
    );
    expect(azureDocsLink).toBeDefined();
    expect(azureDocsLink?.text).toBe('This is the content extracted from the first link about Azure documentation');

    const apiRefLink = result.additionalInfo.links.find(
      (content) => content.url.toString() === 'http://example.com/api-reference'
    );
    expect(apiRefLink).toBeDefined();
    expect(apiRefLink?.text).toBe('This is the content extracted from the second link about API reference');

    const sdkRefLink = result.additionalInfo.links.find(
      (content) => content.url.toString() === 'http://example.com/azure-sdk-ref'
    );
    expect(sdkRefLink).toBeDefined();
    expect(sdkRefLink?.text).toBe('This is the content extracted from the second link about SDK reference');
  });
});
