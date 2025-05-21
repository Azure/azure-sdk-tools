import axios from 'axios';
import config from '../config.js';
import { logger } from '../logging/logger.js';

export interface RAGOptions {
  endpoint: string;
  apiKey: string;
  tenantId: string;
}

interface RAGReference {
  title: string;
  source: string;
  link: string;
  content: string;
}

export interface RAGReply {
  answer: string;
  has_result: boolean;
  references: RAGReference[];
}

// test format
const debugText = `
[DEBUG] reply with format:
    ???

    # hello \`hi-123\` 11111 - 22222
    
    \`\`\` javascript
    const code  = 2 * x;
    function test() {
        xxx("test");
    }
    \`\`\`

- 1112dasd dasdasda
- 1112dasd dasdasda

1. dasdasdasda
2. asdas dsdas
*ffas aa*, **dasda asss**, _sdddas_, __dasdaaaaaa__
    `;

export async function getRAGReply(question: string, options: RAGOptions, meta?: object): Promise<RAGReply | undefined> {
  if (config.debug)
    return {
      answer: debugText,
      has_result: true,
      references: [],
    };

  try {
    const response = await axios.post(
      options.endpoint,
      {
        tenant_id: options.tenantId,
        message: {
          role: 'user',
          content: question,
        },
      },
      {
        headers: {
          'X-API-Key': options.apiKey,
          'Content-Type': 'application/json; charset=utf-8',
        },
      }
    );
    if (response.status !== 200) {
      logger.warn(`Failed to fetch data from RAG backend. Status: ${response.status}`, meta);
    }
    logger.info('Get response from RAG', response.data, meta);
    return response.data;
  } catch (error) {
    logger.warn('Failed to get reply from RAG:', error, meta);
    return undefined;
  }
}
