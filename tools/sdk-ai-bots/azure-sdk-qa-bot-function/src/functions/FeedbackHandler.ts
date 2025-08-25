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

    // Enhanced query method for complex filtering
    async queryFeedback(params: FeedbackQueryParams): Promise<FeedbackData[]> {
        const filters: string[] = [];

        // Filter by channel IDs
        if (params.channelIds && params.channelIds.length > 0) {
            const channelFilters = params.channelIds.map(
                (id) => `PartitionKey eq '${id}'`
            );
            if (channelFilters.length === 1) {
                filters.push(channelFilters[0]);
            } else {
                filters.push(`(${channelFilters.join(" or ")})`);
            }
        }

        // Filter by date range
        if (params.startDate) {
            filters.push(`timestamp ge '${params.startDate}'`);
        }
        if (params.endDate) {
            filters.push(`timestamp le '${params.endDate}'`);
        }

        // Filter by reaction
        if (params.reaction) {
            filters.push(`reaction eq '${params.reaction}'`);
        }

        // Filter by tenant ID
        if (params.tenantId) {
            filters.push(`tenantId eq '${params.tenantId}'`);
        }

        const filter = filters.length > 0 ? filters.join(" and ") : undefined;

        // Query with options
        const queryOptions: any = {};
        if (params.limit) {
            queryOptions.top = params.limit;
        }
        if (filter) {
            queryOptions.filter = filter;
        }

        const entities =
            await this.tableService.queryEntities<FeedbackTableEntity>(
                queryOptions
            );
        return entities.map((entity) => deserializeFeedbackData(entity));
    }

    // Get feedback data by channel ID (all posts in a channel)
    async getFeedbackByChannel(channelId: string): Promise<FeedbackData[]> {
        return this.queryFeedback({ channelIds: [channelId] });
    }

    // Get feedback data by multiple channel IDs
    async getFeedbackByChannels(channelIds: string[]): Promise<FeedbackData[]> {
        return this.queryFeedback({ channelIds });
    }

    // Get feedback data by tenant ID
    async getFeedbackByTenant(tenantId: string): Promise<FeedbackData[]> {
        return this.queryFeedback({ tenantId });
    }

    // Get feedback data by reaction type
    async getFeedbackByReaction(
        reaction: "good" | "bad"
    ): Promise<FeedbackData[]> {
        return this.queryFeedback({ reaction });
    }

    // Get feedback data within a time range
    async getFeedbackByTimeRange(
        startDate: string,
        endDate: string
    ): Promise<FeedbackData[]> {
        return this.queryFeedback({ startDate, endDate });
    }

    // Get all feedback data with optional limit
    async getAllFeedback(limit?: number): Promise<FeedbackData[]> {
        return this.queryFeedback({ limit });
    }
}

// Helper function to parse query parameters
function parseQueryParams(request: HttpRequest): FeedbackQueryParams {
    const url = new URL(request.url);
    const params: FeedbackQueryParams = {};

    // Parse channel IDs
    const channelIds = url.searchParams.get("channelIds");
    if (channelIds) {
        params.channelIds = channelIds.split(",").map((id) => id.trim());
    }

    // Parse channel names (for future channel name to ID mapping)
    const channelNames = url.searchParams.get("channelNames");
    if (channelNames) {
        params.channelNames = channelNames
            .split(",")
            .map((name) => name.trim());
    }

    // Parse date range
    const startDate = url.searchParams.get("startDate");
    if (startDate) {
        params.startDate = startDate;
    }

    const endDate = url.searchParams.get("endDate");
    if (endDate) {
        params.endDate = endDate;
    }

    // Parse reaction
    const reaction = url.searchParams.get("reaction");
    if (reaction && (reaction === "good" || reaction === "bad")) {
        params.reaction = reaction;
    }

    // Parse tenant ID
    const tenantId = url.searchParams.get("tenantId");
    if (tenantId) {
        params.tenantId = tenantId;
    }

    // Parse limit
    const limit = url.searchParams.get("limit");
    if (limit) {
        const parsedLimit = parseInt(limit, 10);
        if (!isNaN(parsedLimit) && parsedLimit > 0) {
            params.limit = parsedLimit;
        }
    }

    return params;
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
                // Parse query parameters
                const queryParams = parseQueryParams(request);

                // Special handling for channel names (if you have a mapping service)
                if (
                    queryParams.channelNames &&
                    queryParams.channelNames.length > 0
                ) {
                    // TODO: Implement channel name to ID mapping
                    // For now, treat channel names as channel IDs
                    if (!queryParams.channelIds) {
                        queryParams.channelIds = [];
                    }
                    queryParams.channelIds.push(...queryParams.channelNames);
                }

                // Query feedback data
                const feedbackData = await feedbackService.queryFeedback(
                    queryParams
                );

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

// Usage example and API documentation:
/*
API Endpoints:

GET /api/getFeedbackData
Query Parameters:
- channelIds: Comma-separated list of channel IDs (e.g., "channel1,channel2")
- channelNames: Comma-separated list of channel names (e.g., "general,support")
- startDate: ISO date string (e.g., "2024-01-01T00:00:00Z")
- endDate: ISO date string (e.g., "2024-12-31T23:59:59Z")
- reaction: "good" or "bad"
- tenantId: Tenant identifier
- limit: Maximum number of results to return

Examples:
- GET /api/getFeedbackData?channelIds=channel123
- GET /api/getFeedbackData?channelIds=channel1,channel2&reaction=good
- GET /api/getFeedbackData?startDate=2024-01-01T00:00:00Z&endDate=2024-01-31T23:59:59Z
- GET /api/getFeedbackData?tenantId=tenant123&limit=100

POST /api/getFeedbackData
Body: FeedbackData object

Response Format:
{
    "success": boolean,
    "data": FeedbackData[],
    "count": number,
    "error"?: string
}
*/

// Register the Azure Function
app.http("getFeedbackData", {
    methods: ["GET"],
    authLevel: "function",
    handler: getFeedbackData,
});
