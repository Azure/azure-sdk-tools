import axios from 'axios';
import { logger } from '../logging/logger.js';
import { ragApiPaths } from '../config/config.js';

export interface RAGOptions {
  endpoint: string;
  apiKey: string;
  tenantId: string;
}

export interface RAGReference {
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

// TODO: reuse function to post request to RAG backend
export async function getRAGReply(
  question: string,
  options: RAGOptions,
  meta: object = {}
): Promise<RAGReply | undefined> {
  logger.info(
    `Post to get reply from RAG on endpoint ${options.endpoint + ragApiPaths.completion} with tenant ${
      options.tenantId
    }`,
    meta
  );
  try {
    const response = await axios.post(
      options.endpoint + ragApiPaths.completion,
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
      logger.warn(`Failed to fetch data from RAG backend. Status: ${response.status}`, { meta });
    }
    logger.info('Get response from RAG', { responseBody: response.data, meta });
    return response.data;
  } catch (error) {
    logger.warn('Failed to get reply from RAG:', { error, meta });
    return undefined;
  }
}

export enum FeedbackReaction {
  good,
  bad,
}

export async function sendFeedback(
  conversation: string[],
  reaction: FeedbackReaction,
  options: RAGOptions,
  meta: object = {}
) {
  logger.info(
    `Post to get reply from RAG on endpoint ${options.endpoint + ragApiPaths.feedback} with tenant ${options.tenantId}`,
    { meta }
  );
  try {
    const response = await axios.post(
      options.endpoint + ragApiPaths.feedback,
      {
        tenant_id: options.tenantId,
        messages: conversation.map((con) => ({
          role: 'user',
          content: con,
        })),
        reaction: reaction.toString(),
      },
      {
        headers: {
          'X-API-Key': options.apiKey,
          'Content-Type': 'application/json; charset=utf-8',
        },
      }
    );
    if (response.status !== 200) {
      logger.warn(`Failed to fetch data from feedback backend. Status: ${response.status}`);
    }
    return response.data;
  } catch (error) {
    logger.warn('Failed to send feedback:', { error, meta });
    return undefined;
  }
}
