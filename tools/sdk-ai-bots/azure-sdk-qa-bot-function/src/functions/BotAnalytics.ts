import {
    app,
    HttpRequest,
    HttpResponseInit,
    InvocationContext,
} from "@azure/functions";
import { TableEntity } from "@azure/data-tables";
import { v4 as uuidv4 } from "uuid";
import { TableService } from "../services/StorageService";

// Message role types
export type Role = "user" | "assistant" | "system";

// Message interface
export interface Message {
    role: Role;
    content: string;
    rawContent?: string; // Optional, raw content for searching
    name?: string; // Optional, sender name for system messages
}

// Feedback data interface matching table columns
export interface FeedbackData {
    timestamp: string;
    tenantId: string;
    messages: Message[]; // Array of Message objects
    reaction: "good" | "bad";
    comment: string;
    reasons: string[];
    link: string; // URL
    postId: string; // Original post ID
    channelId: string; // Maps to PartitionKey
    feedbackId?: string; // GUID that will be used as part of RowKey
}

// Table entity interface (lowercase columns as stored in table)
export interface FeedbackTableEntity extends TableEntity {
    timestamp: string;
    tenantId: string;
    messages: string; // Array will be serialized to string
    reaction: string;
    comment: string;
    reasons: string; // Array will be serialized to string
    link: string;
    postId: string; // Original post ID stored as separate field
}

// Query parameters interface for feedback requests
export interface FeedbackQueryParams {
    channelIds?: string[]; // Array of channel IDs
    channelNames?: string[]; // Array of channel names (will need mapping)
    startDate?: string; // ISO date string
    endDate?: string; // ISO date string
    reaction?: "good" | "bad"; // Filter by reaction
    tenantId?: string; // Filter by tenant
    limit?: number; // Limit number of results
}

// API Response interface
export interface FeedbackApiResponse {
    success: boolean;
    data?: FeedbackData[];
    error?: string;
}

// Serialization function: Convert from interface to table entity
export function serializeFeedbackData(data: FeedbackData): FeedbackTableEntity {
    const feedbackId = data.feedbackId || uuidv4(); // Generate GUID if not provided

    return {
        partitionKey: data.channelId, // ChannelID maps to PartitionKey
        rowKey: feedbackId, // feedbackId (GUID) format
        timestamp: data.timestamp,
        tenantId: data.tenantId,
        messages: JSON.stringify(data.messages), // Serialize array to string
        reaction: data.reaction,
        comment: data.comment,
        reasons: JSON.stringify(data.reasons), // Serialize array to string
        link: data.link,
        postId: data.postId, // Store original post ID as separate field
    };
}

// Deserialization function: Convert from table entity to interface
export function deserializeFeedbackData(
    entity: FeedbackTableEntity
): FeedbackData {
    // rowKey is now directly the feedbackId (GUID)
    const feedbackId = entity.rowKey || "";

    return {
        timestamp: entity.timestamp,
        tenantId: entity.tenantId,
        messages: JSON.parse(entity.messages || "[]"), // Deserialize string to array
        reaction: entity.reaction as "good" | "bad",
        comment: entity.comment,
        reasons: JSON.parse(entity.reasons || "[]"), // Deserialize string to array
        link: entity.link,
        postId: entity.postId,
        channelId: entity.partitionKey || "",
        feedbackId: feedbackId,
    };
}

// Feedback service class that uses the generic TableService
export class FeedbackService {
    private tableService: TableService;

    constructor(tableName: string = "feedback") {
        this.tableService = new TableService(tableName);
    }

    async getAllFeedbackData(): Promise<FeedbackData[]> {
        const entities =
            await this.tableService.queryEntities<FeedbackTableEntity>({});
        return entities.map((entity) => deserializeFeedbackData(entity));
    }
}

// Azure Function: Get Feedback Data
export async function getFeedbackData(
    request: HttpRequest,
    context: InvocationContext
): Promise<HttpResponseInit> {
    context.log("Feedback Handler function processed a request.");

    try {
        // Initialize feedback service (no connection string needed)
        const feedbackService = new FeedbackService("feedback");

        // Handle different HTTP methods
        switch (request.method) {
            case "GET":
                const feedbackData = await feedbackService.getAllFeedbackData();

                return {
                    status: 200,
                    jsonBody: {
                        success: true,
                        data: feedbackData,
                    } as FeedbackApiResponse,
                };

            default:
                return {
                    status: 405,
                    jsonBody: {
                        success: false,
                        error: "Method not allowed",
                    } as FeedbackApiResponse,
                };
        }
    } catch (error) {
        context.log("Error processing feedback request:", error);

        return {
            status: 500,
            jsonBody: {
                success: false,
                error:
                    error instanceof Error
                        ? error.message
                        : "Internal server error",
            } as FeedbackApiResponse,
        };
    }
}

// Register the Azure Function
app.http("feedbacks", {
    methods: ["GET"],
    authLevel: "function",
    handler: getFeedbackData,
});
