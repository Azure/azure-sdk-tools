import axios from 'axios';
import { logger } from '../logging/logger.js';
import { ragApiPaths } from '../config/config.js';

export interface RAGOptions {
  endpoint: string;
  apiKey: string;
}

// Source definitions
export type Source =
  | 'typespec_docs'
  | 'typespec_azure_docs'
  | 'azure_rest_api_specs_wiki'
  | 'azure_sdk_for_python_docs'
  | 'azure_sdk_for_python_wiki'
  | 'static_typespec_qa'
  | 'azure_api_guidelines'
  | 'azure_resource_manager_rpc';

// Role definitions
export type Role = 'user' | 'assistant' | 'system';

// Message interface
export interface Message {
  role: Role;
  content: string;
  raw_content?: string;
  name?: string;
}

// Reference interface
export interface Reference {
  title: string;
  source: string;
  link: string;
  content: string;
}

// Additional info type definitions
export type AdditionalInfoType = 'link' | 'image';

// Additional info interface
export interface AdditionalInfo {
  type: AdditionalInfoType;
  content: string;
  link: string; // required if type is link
}

// Completion request interface
export interface CompletionRequestPayload {
  tenant_id: string;
  prompt_template?: string;
  prompt_template_arguments?: string;
  top_k?: number; // default is 10
  sources?: Source[]; // default is all
  message: Message;
  history?: Message[];
  with_full_context?: boolean; // default is false
  with_preprocess?: boolean; // default is false
  additional_infos?: AdditionalInfo[];
}

export type QuestionCategory = 'unknown' | 'branded' | 'unbranded';

// Intension result interface (referenced in CompletionResp)
export interface IntensionResult {
  question: string;
  category: QuestionCategory;
}

// Completion response interface
export interface CompletionResponsePayload {
  id: string;
  answer: string;
  has_result: boolean;
  references?: Reference[];
  full_context?: string;
  intension?: IntensionResult;
}

export type Reaction = 'good' | 'bad';

// Feedback request interface
export interface FeedbackRequestPayload {
  tenant_id: string;
  messages: Message[];
  reaction: Reaction;
  comment?: string;
  reasons?: string[];
}

// TODO: reuse function to post request to RAG backend
export async function getRAGReply(
  payload: CompletionRequestPayload,
  options: RAGOptions,
  meta: object
): Promise<CompletionResponsePayload | undefined> {
  logger.info(
    `Post to get reply from RAG on endpoint ${options.endpoint + ragApiPaths.completion} with tenant ${
      payload.tenant_id
    }`,
    meta
  );
  try {
    logger.info('Completion payload:', { payload });

    const response = await axios.post(options.endpoint + ragApiPaths.completion, payload, {
      headers: {
        'X-API-Key': options.apiKey,
        'Content-Type': 'application/json; charset=utf-8',
      },
    });
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

export async function sendFeedback(payload: FeedbackRequestPayload, options: RAGOptions, meta: object) {
  logger.info(
    `Post to get reply from RAG on endpoint ${options.endpoint + ragApiPaths.feedback} with tenant ${
      payload.tenant_id
    }`,
    { meta }
  );
  try {
    const response = await axios.post(options.endpoint + ragApiPaths.feedback, payload, {
      headers: {
        'X-API-Key': options.apiKey,
        'Content-Type': 'application/json; charset=utf-8',
      },
    });
    if (response.status !== 200) {
      logger.warn(`Failed to fetch data from feedback backend. Status: ${response.status}`);
    }
    return response.data;
  } catch (error) {
    logger.warn('Failed to send feedback:', { error, meta });
    return undefined;
  }
}
