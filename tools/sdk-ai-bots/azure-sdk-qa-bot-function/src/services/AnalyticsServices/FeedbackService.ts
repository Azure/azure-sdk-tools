import { TableEntity } from "@azure/data-tables";
import { AnalyticsServiceBase } from "./AnalyticsServiceBase";
import { TableService } from "../StorageService";

// Message role types
type Role = "user" | "assistant" | "system";

// Feedback data interface matching table columns
interface FeedbackData {
    submitTime: string;
    tenantId: string;
    messages: Message[]; // Array of Message objects
    reaction: "good" | "bad";
    comment: string;
    reasons: string[];
    link: string; // URL
    postId: string; // Original post ID
    channelId: string; // Maps to PartitionKey
}

// Message interface
interface Message {
    role: Role;
    content: string;
    rawContent?: string; // Optional, raw content for searching
    name?: string; // Optional, sender name for system messages
}

// Table entity interface (lowercase columns as stored in table)
interface FeedbackTableEntity extends TableEntity {
    submitTime: string;
    tenantId: string;
    messages: string; // Array will be serialized to string
    reaction: string;
    comment: string;
    reasons: string; // Array will be serialized to string
    link: string;
    postId: string; // Original post ID stored as separate field
}

// Feedback service class that uses the generic TableService
export class FeedbackService extends AnalyticsServiceBase<
    FeedbackTableEntity,
    FeedbackData
> {
    private readonly tableName = "Feedback";
    private tableService = new TableService(this.tableName);

    // Deserialization function: Convert from table entity to interface
    protected deserializeData(entities: FeedbackTableEntity[]): FeedbackData[] {
        return entities.map((entity) => ({
            submitTime: entity.submitTime,
            tenantId: entity.tenantId,
            messages: JSON.parse(entity.messages || "[]"), // Deserialize string to array
            reaction: entity.reaction as "good" | "bad",
            comment: entity.comment,
            reasons: JSON.parse(entity.reasons || "[]"), // Deserialize string to array
            link: entity.link,
            postId: entity.postId,
            channelId: entity.partitionKey || "",
        }));
    }

    protected async getEntities(): Promise<FeedbackTableEntity[]> {
        return this.tableService.queryEntities<FeedbackTableEntity>({});
    }
}
