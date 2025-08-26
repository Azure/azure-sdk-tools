import { v4 as uuidv4 } from "uuid";
import { TableEntity } from "@azure/data-tables";
import { Converter, loadChannelMapping } from "./utils";

interface RecordRow {
    Timestamp?: string;
    ChannelName?: string;
    ChannelID?: string;
    MessageLink?: string;
}

interface RecordEntity extends TableEntity {
    partitionKey: string; // channelId
    rowKey: string; // GUID
    submitTime: string;
    channelName: string; // channelName field from channel.yaml
    postId: string; // postId field for easier querying
    link: string;
}

class RecordConverter extends Converter {
    private channelMapping = new Map<string, string>();

    constructor(tableName: string) {
        super(tableName);
    }

    public async init() {
        this.channelMapping = await loadChannelMapping();
    }

    public convertExcelRowToTableEntity(row: RecordRow): RecordEntity {
        // Validate required fields first
        if (!row.MessageLink || row.MessageLink.trim() === "") {
            throw new Error("MessageLink is required and cannot be empty");
        }

        if (!row.ChannelID || row.ChannelID.trim() === "") {
            throw new Error("ChannelID is required and cannot be empty");
        }

        if (!row.Timestamp || row.Timestamp.trim() === "") {
            throw new Error("Timestamp is required and cannot be empty");
        }

        if (!row.ChannelName || row.ChannelName.trim() === "") {
            const channelName = this.channelMapping.get(row.ChannelID);
            if (!channelName) {
                console.warn(
                    `Warning: Channel ID '${row.ChannelID}' not found in mapping.`
                );
            }
            row.ChannelName = channelName || "";
        }

        return {
            partitionKey: row.ChannelID,
            rowKey: uuidv4(),
            submitTime: row.Timestamp,
            channelName: row.ChannelName,
            postId: this.getPostId(row.MessageLink),
            link: row.MessageLink,
        };
    }

    private getPostId(link: string): string {
        if (!link || link.trim() === "") {
            throw new Error("Link is required and cannot be empty");
        }
        const url = new URL(link);
        const postId = url.searchParams.get("parentMessageId");
        return postId;
    }
}

// Main execution function
async function main() {
    try {
        const args = process.argv.slice(2);
        const converter = new RecordConverter("ConversationRecord");
        await converter.init();
        const directory = args[0];
        const readFilePromise = converter.readLocalExcelFiles(directory);
        await converter.convert(
            readFilePromise,
            converter.convertExcelRowToTableEntity.bind(converter)
        );
    } catch (error) {
        console.error("Conversion failed:", error);
        process.exit(1);
    }
}

if (require.main === module) {
    main();
}
