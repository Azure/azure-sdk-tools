import { TableClient, TableServiceClient, TableEntity, odata } from '@azure/data-tables';
import { DefaultAzureCredential, ManagedIdentityCredential } from '@azure/identity';
import { logger } from '../logging/logger.js';
import config from '../config/config.js';
import { RAGReply } from '../backend/rag.js';

export interface Prompt {
  textWithoutMention: string;
  links?: string[];
  images?: string[];
  userName: string;
  timestamp: Date;
}

/**
 * Interface representing a message in a conversation
 */
export interface ConversationMessage {
  /**
   * The ID of the conversation the message belongs to
   */
  conversationId: string;

  /**
   * The unique ID of the activity (message)
   */
  activityId: string;

  /**
   * Text content of the message
   */
  text?: string;

  /**
   * RAG reply
   */
  reply?: RAGReply;

  /**
   * Raw prompt information
   */
  prompt?: Prompt;

  /**
   * Timestamp when the message was created
   */
  timestamp: Date;
}

/**
 * Azure Table Storage entity representing a conversation message
 */
export interface MessageEntity extends TableEntity {
  /**
   * Partition key - conversation ID
   */
  partitionKey: string;

  /**
   * Row key - activity/message ID
   */
  rowKey: string;

  /**
   * Text content of the message
   */
  text: string;

  /**
   * JSON string of prompt
   */
  prompt?: string;

  /**
   * JSON string of RAG reply
   */
  reply?: string;

  /**
   * ISO timestamp string when the message was created
   */
  timestamp: string;
}

/**
 * Handles saving and retrieving conversation messages to/from Azure Table Storage
 */
export class ConversationHandler {
  private tableClient: TableClient | undefined;
  private readonly logMeta: object;
  private readonly botId: string;
  private readonly tableStorageUrl: string;
  private readonly tableName: string;

  /**
   * Creates a new instance of the ConversationHandler class
   * @param logMeta Logging metadata
   */
  constructor(logMeta: object = {}) {
    this.logMeta = logMeta;
    this.botId = config.MicrosoftAppId;

    // Get Azure Table Storage configuration from config
    this.tableStorageUrl = config.azureStorageUrl;
    this.tableName = config.azureTableNameForConversation;

    logger.info('ConversationHandler initialized', {
      url: this.tableStorageUrl,
      table: this.tableName,
      meta: this.logMeta,
    });
  }

  /**
   * Initializes the table client
   */
  public async initialize(): Promise<void> {
    try {
      if (!this.tableStorageUrl) {
        logger.warn('Azure Table Storage URL not configured. Message persistence is disabled.', { meta: this.logMeta });
        return;
      }

      // Use managed identity or default Azure credentials
      const credential =
        process.env.IS_LOCAL === 'true' ? new DefaultAzureCredential() : new ManagedIdentityCredential(this.botId);

      try {
        // Create table service client
        const serviceClient = new TableServiceClient(this.tableStorageUrl, credential);

        // Create the table if it doesn't exist
        await serviceClient.createTable(this.tableName);

        // Create table client for this table
        this.tableClient = new TableClient(this.tableStorageUrl, this.tableName, credential);

        logger.info('Azure Table Storage connection initialized successfully', { meta: this.logMeta });
      } catch (tableError: any) {
        // If the table already exists, we can ignore that specific error
        if (tableError.statusCode === 409 && tableError.errorCode === 'TableAlreadyExists') {
          this.tableClient = new TableClient(this.tableStorageUrl, this.tableName, credential);
          logger.info('Connected to existing Azure Table Storage table', { meta: this.logMeta });
        } else {
          throw tableError;
        }
      }
    } catch (error) {
      logger.error('Failed to initialize Azure Table Storage connection', { error, meta: this.logMeta });
      throw error;
    }
  }

  /**
   * Saves a conversation message to Azure Table Storage
   * @param message The message to save
   * @returns The saved message with any server-generated properties
   */
  public async saveMessage(message: ConversationMessage): Promise<MessageEntity | undefined> {
    if (!this.tableClient) {
      logger.warn('Table client not initialized. Call initialize() before saving messages.', { meta: this.logMeta });
      return;
    }

    try {
      // Convert ConversationMessage to MessageEntity
      const entity = this.messageToEntity(message);

      // Save the entity to Azure Table Storage
      await this.tableClient.createEntity(entity);

      logger.info('Message saved to Azure Table Storage', {
        conversationId: message.conversationId,
        messageId: message.activityId,
        meta: this.logMeta,
        entity: JSON.stringify(entity),
      });

      return entity;
    } catch (error) {
      logger.error('Failed to save message to Azure Table Storage', {
        error,
        conversationId: message.conversationId,
        messageId: message.activityId,
        meta: this.logMeta,
      });
      return;
    }
  }

  /**
   * Gets all messages for a specific conversation
   * @param conversationId The ID of the conversation
   * @returns Array of conversation messages
   */
  public async getConversationMessages(conversationId: string): Promise<ConversationMessage[]> {
    if (!this.tableClient) {
      logger.warn('Table client not initialized. Call initialize() before retrieving messages.', {
        meta: this.logMeta,
      });
      return [];
    }

    try {
      // Query entities with the specified partition key (conversationId)
      const entities = this.tableClient.listEntities({
        queryOptions: { filter: odata`PartitionKey eq ${conversationId}` },
      });

      const messages: ConversationMessage[] = [];

      // Process all entities
      for await (const entity of entities) {
        const messageEntity = entity as unknown as MessageEntity;
        messages.push(this.entityToMessage(messageEntity));
      }

      // Sort by timestamp
      const sortedMessages = messages.sort((a, b) => {
        return (a.timestamp?.getTime() || 0) - (b.timestamp?.getTime() || 0);
      });

      return sortedMessages;
    } catch (error) {
      logger.error('Failed to retrieve conversation messages', {
        error,
        conversationId,
        meta: this.logMeta,
      });
      return [];
    }
  }

  /**
   * Converts a ConversationMessage to a MessageEntity for storage
   * @param message The conversation message to convert
   * @returns MessageEntity ready for storage
   */
  private messageToEntity(message: ConversationMessage): MessageEntity {
    const timestamp = message.timestamp || new Date();

    // Serialize prompt, handling Date objects properly
    const promptToStore = message.prompt
      ? {
          ...message.prompt,
          timestamp: message.prompt.timestamp?.toISOString(),
        }
      : undefined;

    return {
      partitionKey: message.conversationId,
      rowKey: message.activityId,
      text: message.text || '',
      timestamp: timestamp.toISOString(),
      prompt: promptToStore ? JSON.stringify(promptToStore) : undefined,
      reply: message.reply ? JSON.stringify(message.reply) : undefined,
    };
  }

  /**
   * Converts a MessageEntity from storage to a ConversationMessage
   * @param entity The entity from storage
   * @returns ConversationMessage domain object
   */
  private entityToMessage(entity: MessageEntity): ConversationMessage {
    let prompt: Prompt | undefined;
    let reply: RAGReply | undefined;

    // Parse prompt and handle Date conversion
    if (entity.prompt) {
      const parsedPrompt = JSON.parse(entity.prompt);
      if (parsedPrompt && Object.keys(parsedPrompt).length > 0) {
        prompt = {
          ...parsedPrompt,
          timestamp: parsedPrompt.timestamp ? new Date(parsedPrompt.timestamp) : undefined,
        } as Prompt;
      }
    }

    // Parse reply
    if (entity.reply) {
      const parsedReply = JSON.parse(entity.reply);
      if (parsedReply && Object.keys(parsedReply).length > 0) {
        reply = parsedReply as RAGReply;
      }
    }

    return {
      conversationId: entity.partitionKey,
      activityId: entity.rowKey,
      text: entity.text,
      timestamp: new Date(entity.timestamp),
      prompt: prompt,
      reply: reply,
    };
  }
}
