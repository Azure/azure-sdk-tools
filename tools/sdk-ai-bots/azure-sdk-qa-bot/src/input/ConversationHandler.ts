import { TableClient, TableServiceClient, TableEntity, odata } from '@azure/data-tables';
import { DefaultAzureCredential, ManagedIdentityCredential } from '@azure/identity';
import { logger } from '../logging/logger.js';
import config from '../config/config.js';

type MessageType = 'question' | 'answer';

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
  text: string;

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
      ...this.logMeta,
    });
  }

  /**
   * Initializes the table client
   */
  public async initialize(): Promise<void> {
    try {
      if (!this.tableStorageUrl) {
        logger.warn('Azure Table Storage URL not configured. Message persistence is disabled.', this.logMeta);
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

        logger.info('Azure Table Storage connection initialized successfully', this.logMeta);
      } catch (tableError: any) {
        // If the table already exists, we can ignore that specific error
        if (tableError.statusCode === 409 && tableError.errorCode === 'TableAlreadyExists') {
          this.tableClient = new TableClient(this.tableStorageUrl, this.tableName, credential);
          logger.info('Connected to existing Azure Table Storage table', this.logMeta);
        } else {
          throw tableError;
        }
      }
    } catch (error) {
      logger.error('Failed to initialize Azure Table Storage connection', { error, ...this.logMeta });
      throw error;
    }
  }

  /**
   * Saves a conversation message to Azure Table Storage
   * @param message The message to save
   * @returns The saved message with any server-generated properties
   */
  public async saveMessage(message: ConversationMessage): Promise<ConversationMessage> {
    if (!this.tableClient) {
      logger.warn('Table client not initialized. Call initialize() before saving messages.', this.logMeta);
    }

    try {
      // Convert ConversationMessage to MessageEntity
      const entity = this.messageToEntity(message);

      // Save the entity to Azure Table Storage
      await this.tableClient.createEntity(entity);

      logger.info('Message saved to Azure Table Storage', {
        conversationId: message.conversationId,
        messageId: message.activityId,
        ...this.logMeta,
      });

      return {
        ...message,
        timestamp: new Date(entity.timestamp),
      };
    } catch (error) {
      logger.error('Failed to save message to Azure Table Storage', {
        error,
        conversationId: message.conversationId,
        messageId: message.activityId,
        ...this.logMeta,
      });
      return message;
    }
  }

  /**
   * Gets all messages for a specific conversation
   * @param conversationId The ID of the conversation
   * @returns Array of conversation messages
   */
  public async getConversationMessages(conversationId: string): Promise<ConversationMessage[]> {
    if (!this.tableClient) {
      logger.warn('Table client not initialized. Call initialize() before retrieving messages.', this.logMeta);
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
      messages.sort((a, b) => {
        return (a.timestamp?.getTime() || 0) - (b.timestamp?.getTime() || 0);
      });

      return messages;
    } catch (error) {
      logger.error('Failed to retrieve conversation messages', {
        error,
        conversationId,
        ...this.logMeta,
      });
      return [];
    }
  }

  /**
   * Gets a specific message by its ID and conversation ID
   * @param conversationId The ID of the conversation
   * @param messageId The ID of the message
   * @returns The message if found, otherwise undefined
   */
  public async getMessage(conversationId: string, messageId: string): Promise<ConversationMessage | undefined> {
    if (!this.tableClient) {
      logger.warn('Table client not initialized. Call initialize() before retrieving messages.', this.logMeta);
      return undefined;
    }

    try {
      // Try to retrieve the entity by partitionKey (conversationId) and rowKey (messageId)
      try {
        const entity = await this.tableClient.getEntity(conversationId, messageId);
        const messageEntity = entity as unknown as MessageEntity;

        // Convert the entity to a ConversationMessage
        return this.entityToMessage(messageEntity);
      } catch (err: any) {
        // If entity not found, Azure SDK throws an error with statusCode 404
        if (err.statusCode === 404) {
          return undefined;
        }
        throw err; // Re-throw if it's a different error
      }
    } catch (error) {
      logger.error('Failed to retrieve message', {
        error,
        conversationId,
        messageId,
        ...this.logMeta,
      });
      return undefined;
    }
  }

  /**
   * Converts a ConversationMessage to a MessageEntity for storage
   * @param message The conversation message to convert
   * @returns MessageEntity ready for storage
   */
  private messageToEntity(message: ConversationMessage): MessageEntity {
    const timestamp = message.timestamp || new Date();

    return {
      partitionKey: message.conversationId,
      rowKey: message.activityId,
      text: message.text,
      timestamp: timestamp.toISOString(),
    };
  }

  /**
   * Converts a MessageEntity from storage to a ConversationMessage
   * @param entity The entity from storage
   * @returns ConversationMessage domain object
   */
  private entityToMessage(entity: MessageEntity): ConversationMessage {
    return {
      conversationId: entity.partitionKey,
      activityId: entity.rowKey,
      text: entity.text,
      timestamp: new Date(entity.timestamp),
    };
  }
}
