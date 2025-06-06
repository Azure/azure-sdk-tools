import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PromptGenerator } from '../src/input/promptGenerator.js';

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

// Mock config
vi.mock('../src/config/config.js', () => {
  return {
    default: {
      azureComputerVisionEndpoint: 'https://test-endpoint.cognitiveservices.azure.com/',
      azureComputerVisionApiKey: 'test-key',
      MicrosoftAppId: 'test-bot-id',
    },
  };
});

describe('PromptGenerator', () => {
  let promptGenerator: PromptGenerator;

  beforeEach(() => {
    promptGenerator = new PromptGenerator({ test: 'meta' });
  });

  it('should generate a plain full prompt with conversation messages', async () => {
    const prompt = {
      textWithoutMention: 'Test prompt',
      links: ['http://example.com/azure-docs', 'http://example.com/api-reference'],
      images: ['http://example.com/image1.jpg', 'http://example.com/image2.jpg'],
      userName: 'User',
      timestamp: new Date('2025-10-01T12:00:00Z'),
    };
    const conversationMessages = [
      {
        conversationId: 'conv1',
        activityId: 'act1',
        prompt: {
          textWithoutMention: 'Previous prompt',
          links: ['http://example.com/previous-link'],
          images: ['http://example.com/previous-image.png'],
          userName: 'User1',
          timestamp: new Date('2023-10-01T12:00:00Z'),
        },
        reply: {
          answer: 'Previous answer',
          references: [
            {
              title: 'Azure SDK Documentation',
              source: 'https://docs.microsoft.com/azure/sdk',
              link: 'https://docs.microsoft.com/azure/sdk',
              content: 'This is content from Azure SDK documentation that was referenced in the previous answer.',
            },
            {
              title: 'API Reference Guide',
              source: 'https://docs.microsoft.com/api-reference',
              link: 'https://docs.microsoft.com/api-reference',
              content: 'This is content from the API reference guide used in the previous response.',
            },
          ],
          has_result: true,
        },
        timestamp: new Date('2023-10-01T12:00:00Z'),
      },
      {
        conversationId: 'conv2',
        activityId: 'act2',
        prompt: {
          textWithoutMention: 'Another previous prompt',
          links: ['http://example.com/another-link'],
          images: ['http://example.com/another-image.gif'],
          userName: 'User2',
          timestamp: new Date('2020-10-01T12:00:00Z'),
        },
        reply: {
          answer: 'Another previous answer',
          references: [
            {
              title: 'Azure Best Practices',
              source: 'https://docs.microsoft.com/azure/best-practices',
              link: 'https://docs.microsoft.com/azure/best-practices',
              content: 'This is content about Azure best practices from the documentation.',
            },
            {
              title: 'Troubleshooting Guide',
              source: 'https://docs.microsoft.com/azure/troubleshooting',
              link: 'https://docs.microsoft.com/azure/troubleshooting',
              content: 'This is troubleshooting information that was used in the previous response.',
            },
          ],
          has_result: true,
        },
        timestamp: new Date('2020-10-01T12:00:00Z'),
      },
    ];

    const fullPrompt = await promptGenerator.generatePlainFullPrompt(prompt, conversationMessages);

    expect(fullPrompt)
      .toEqual(`# Question from user User on date: Wed Oct 01 2025 20:00:00 GMT+0800 (China Standard Time)
Test prompt
## Additional information from images
### Content from image-0: http://example.com/image1.jpg
This is extracted text from the first image
### Content from image-1: http://example.com/image2.jpg
This is extracted text from the second image
## Additional information from links
### Content from link-0: http://example.com/azure-docs
This is the content extracted from the first link about Azure documentation
### Content from link-1: http://example.com/api-reference
This is the content extracted from the second link about API reference

# Appendix: Previous conversations
## Conversation question & answer on date: Sun Oct 01 2023 20:00:00 GMT+0800 (China Standard Time)
### Question from user User1 on date: Sun Oct 01 2023 20:00:00 GMT+0800 (China Standard Time)
Previous prompt
#### Additional information from images
##### Content from image-0: http://example.com/image1.jpg
This is extracted text from the first image
##### Content from image-1: http://example.com/image2.jpg
This is extracted text from the second image
#### Additional information from links
##### Content from link-0: http://example.com/azure-docs
This is the content extracted from the first link about Azure documentation
##### Content from link-1: http://example.com/api-reference
This is the content extracted from the second link about API reference
### AI Reply
#### Answer
Previous answer
#### Has result
Yes
#### References
##### Title
Azure SDK Documentation
##### Source
https://docs.microsoft.com/azure/sdk
##### Link
https://docs.microsoft.com/azure/sdk
##### Content
This is content from Azure SDK documentation that was referenced in the previous answer.
##### Title
API Reference Guide
##### Source
https://docs.microsoft.com/api-reference
##### Link
https://docs.microsoft.com/api-reference
##### Content
This is content from the API reference guide used in the previous response.
## Conversation question & answer on date: Thu Oct 01 2020 20:00:00 GMT+0800 (China Standard Time)
### Question from user User2 on date: Thu Oct 01 2020 20:00:00 GMT+0800 (China Standard Time)
Another previous prompt
#### Additional information from images
##### Content from image-0: http://example.com/image1.jpg
This is extracted text from the first image
##### Content from image-1: http://example.com/image2.jpg
This is extracted text from the second image
#### Additional information from links
##### Content from link-0: http://example.com/azure-docs
This is the content extracted from the first link about Azure documentation
##### Content from link-1: http://example.com/api-reference
This is the content extracted from the second link about API reference
### AI Reply
#### Answer
Another previous answer
#### Has result
Yes
#### References
##### Title
Azure Best Practices
##### Source
https://docs.microsoft.com/azure/best-practices
##### Link
https://docs.microsoft.com/azure/best-practices
##### Content
This is content about Azure best practices from the documentation.
##### Title
Troubleshooting Guide
##### Source
https://docs.microsoft.com/azure/troubleshooting
##### Link
https://docs.microsoft.com/azure/troubleshooting
##### Content
This is troubleshooting information that was used in the previous response.
`);
  });
});
