import { TableEntity } from "@azure/data-tables";
import { v4 as uuidv4 } from "uuid";
import { Converter, loadChannelMapping } from "./utils";

interface FeedbackTableEntity extends TableEntity {
    partitionKey: string; // channelId
    rowKey: string; // feedbackId (GUID)
    submitTime: string;
    tenantId: string;
    messages: string; // JSON serialized Message[]
    reaction: "good" | "bad";
    comment: string;
    reasons: string; // JSON serialized string[]
    link: string;
    postId: string; // postId field for easier querying
    channelName: string; // channelName field from channel.yaml
}

// Excel row interface (based on the columns: Timestamp TenantID Messages Reaction Comment Reasons Link)
interface ExcelFeedbackRow {
    Timestamp?: string;
    TenantID?: string;
    Messages?: string;
    Reaction?: string;
    Comment?: string;
    Reasons?: string;
    Link?: string;
}

export class FeedbackConverter extends Converter {
    constructor(tableName: string) {
        super(tableName);
    }

    // Convert Excel row directly to table entity (no need to deserialize/serialize)
    public convertExcelRowToTableEntity(
        row: ExcelFeedbackRow,
        channelMapping: Map<string, string>
    ): FeedbackTableEntity {
        try {
            // Validate required fields first
            if (!row.Link || row.Link.trim() === "") {
                throw new Error("Link is required and cannot be empty");
            }

            if (!row.TenantID || row.TenantID.trim() === "") {
                throw new Error("TenantID is required and cannot be empty");
            }

            if (!row.Timestamp || row.Timestamp.trim() === "") {
                throw new Error("Timestamp is required and cannot be empty");
            }

            // Extract channelId and postId from Teams link (will throw error if invalid)
            const { channelId, postId } = this.parseTeamsLink(row.Link);

            // Validate reaction
            const reaction = row.Reaction?.toLowerCase();
            if (reaction !== "good" && reaction !== "bad") {
                throw new Error(
                    `Invalid reaction: ${row.Reaction}. Must be 'good' or 'bad'`
                );
            }

            const channelName = this.getChannelName(channelId, channelMapping);

            return {
                partitionKey: channelId,
                rowKey: uuidv4(),
                submitTime: row.Timestamp,
                tenantId: row.TenantID,
                messages: row.Messages || "", // Use raw string from Excel
                reaction: reaction as "good" | "bad",
                comment: row.Comment || "",
                reasons: row.Reasons || "", // Use raw string from Excel
                link: row.Link,
                postId: postId,
                channelName: channelName,
            };
        } catch (error) {
            console.error("Error converting Excel row:", error, row);
            throw error; // Re-throw the error instead of returning null
        }
    }

    // Get channel name by channelId
    private getChannelName(
        channelId: string,
        channelMapping: Map<string, string>
    ): string {
        return channelMapping.get(channelId) || "";
    }

    // Parse Teams link to extract channelId and postId
    private parseTeamsLink(link: string): {
        channelId: string;
        postId: string;
    } {
        if (!link || link.trim() === "") {
            throw new Error("Link is required and cannot be empty");
        }

        try {
            // Teams link format: https://teams.microsoft.com/l/message/{channelId}/{postId}?tenantId=...
            const url = new URL(link);
            const pathParts = url.pathname.split("/");

            // Find the message path: /l/message/{channelId}/{postId}
            const messageIndex = pathParts.indexOf("message");
            if (messageIndex >= 0 && pathParts.length > messageIndex + 2) {
                const channelId = pathParts[messageIndex + 1];
                const postId = pathParts[messageIndex + 2];

                if (
                    !channelId ||
                    !postId ||
                    channelId.trim() === "" ||
                    postId.trim() === ""
                ) {
                    throw new Error(
                        `Invalid Teams link format: cannot extract channelId or postId from ${link}`
                    );
                }

                return { channelId, postId };
            }

            throw new Error(`Invalid Teams link format: ${link}`);
        } catch (error) {
            if (error instanceof Error) {
                throw error; // Re-throw our custom errors
            }
            throw new Error(`Error parsing Teams link: ${link} - ${error}`);
        }
    }
}

// Main execution function
async function main() {
    try {
        const args = process.argv.slice(2);
        const converter = new FeedbackConverter("Feedback");
        const directory = args[0];
        const readFilePromise = converter.readLocalExcelFiles(directory);
        const channelMapping = await loadChannelMapping();
        await converter.convert(readFilePromise, (row) =>
            converter.convertExcelRowToTableEntity(row, channelMapping)
        );
    } catch (error) {
        console.error("Conversion failed:", error);
        process.exit(1);
    }
}

if (require.main === module) {
    main();
}
