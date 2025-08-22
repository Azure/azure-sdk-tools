import { BlobServiceClient } from "@azure/storage-blob";
import { TableClient, TableEntity } from "@azure/data-tables";
import {
    ChainedTokenCredential,
    ManagedIdentityCredential,
    EnvironmentCredential,
    AzureCliCredential,
} from "@azure/identity";
import * as XLSX from "xlsx";
import { v4 as uuidv4 } from "uuid";
import * as readline from "readline";

// Type definitions (copied from FeedbackHandler to avoid imports)
export type Role = "user" | "assistant" | "system";

export interface Message {
    role: Role;
    content: string;
}

export interface FeedbackData {
    timestamp: string;
    tenantId: string;
    messages: Message[];
    reaction: "good" | "bad";
    comment: string;
    reasons: string[];
    link: string;
    postId: string;
    channelId: string;
    feedbackId: string;
}

export interface FeedbackTableEntity extends TableEntity {
    partitionKey: string; // channelId
    rowKey: string; // feedbackId (GUID)
    timestamp: string;
    tenantId: string;
    messages: string; // JSON serialized Message[]
    reaction: "good" | "bad";
    comment: string;
    reasons: string; // JSON serialized string[]
    link: string;
    postId: string; // postId field for easier querying
}

// Excel row interface (based on the columns: Timestamp TenantID Messages Reaction Comment Reasons Link)
export interface ExcelFeedbackRow {
    Timestamp?: string;
    TenantID?: string;
    Messages?: string;
    Reaction?: string;
    Comment?: string;
    Reasons?: string;
    Link?: string;
}

export class FeedbackConverter {
    private blobServiceClient: BlobServiceClient;
    private tableClient: TableClient;
    private readonly batchSize = 10000; // Azure Table Storage batch limit
    private readonly batchDelayMs = 1000; // 1 second delay between batches

    constructor() {
        const storageAccountName = process.env.AZURE_STORAGE_ACCOUNT_NAME;
        if (!storageAccountName) {
            throw new Error(
                "AZURE_STORAGE_ACCOUNT_NAME environment variable is required"
            );
        }

        // Use ChainedTokenCredential for better fallback options
        const credential = new ChainedTokenCredential(
            new ManagedIdentityCredential(),
            new EnvironmentCredential(),
            new AzureCliCredential()
        );

        const accountUrl = `https://${storageAccountName}.blob.core.windows.net`;
        this.blobServiceClient = new BlobServiceClient(accountUrl, credential);

        const tableUrl = `https://${storageAccountName}.table.core.windows.net`;
        this.tableClient = new TableClient(tableUrl, "feedback", credential);
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

    // Convert Excel row directly to table entity (no need to deserialize/serialize)
    private convertExcelRowToTableEntity(
        row: ExcelFeedbackRow
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

            const feedbackId = uuidv4();

            return {
                partitionKey: channelId,
                rowKey: feedbackId,
                timestamp: row.Timestamp,
                tenantId: row.TenantID,
                messages: row.Messages || "", // Use raw string from Excel
                reaction: reaction as "good" | "bad",
                comment: row.Comment || "",
                reasons: row.Reasons || "", // Use raw string from Excel
                link: row.Link,
                postId: postId,
            };
        } catch (error) {
            console.error("Error converting Excel row:", error, row);
            throw error; // Re-throw the error instead of returning null
        }
    }

    /**
     * Waits for user confirmation before proceeding
     */
    private async waitForUserConfirmation(message: string): Promise<void> {
        const rl = readline.createInterface({
            input: process.stdin,
            output: process.stdout,
        });

        return new Promise((resolve) => {
            rl.question(`${message} (Press Enter to continue): `, () => {
                rl.close();
                resolve();
            });
        });
    }

    // Batch upload entities to table storage
    private async batchUploadEntities(
        entities: FeedbackTableEntity[]
    ): Promise<void> {
        const totalBatches = Math.ceil(entities.length / this.batchSize);

        for (let i = 0; i < entities.length; i += this.batchSize) {
            const batch = entities.slice(i, i + this.batchSize);
            const batchNumber = Math.floor(i / this.batchSize) + 1;

            console.log(
                `Uploading batch ${batchNumber}/${totalBatches} (${batch.length} entities)...`
            );

            // Group entities by partitionKey for batch transactions
            const partitionGroups = new Map<string, FeedbackTableEntity[]>();
            for (const entity of batch) {
                const key = entity.partitionKey;
                if (!partitionGroups.has(key)) {
                    partitionGroups.set(key, []);
                }
                partitionGroups.get(key)!.push(entity);
            }

            // Process each partition group as a separate transaction
            const transactionPromises: Promise<void>[] = [];
            for (const [partitionKey, groupEntities] of partitionGroups) {
                const transactionPromise = (async () => {
                    try {
                        await this.tableClient.submitTransaction(
                            groupEntities.map((entity) => ["upsert", entity])
                        );
                        // Transaction completed successfully
                    } catch (error) {
                        throw new Error(
                            `Failed to submit transaction for partition ${partitionKey}: ${
                                error instanceof Error
                                    ? error.message
                                    : "Unknown error"
                            }`
                        );
                    }
                })();
                transactionPromises.push(transactionPromise);
            }

            // Wait for all transactions in this batch to complete
            await Promise.all(transactionPromises);

            console.log(`Batch ${batchNumber} completed`);

            // Add delay between batches (except for the last batch)
            if (batchNumber < totalBatches) {
                console.log(
                    `Waiting ${this.batchDelayMs}ms before next batch...`
                );
                await new Promise((resolve) =>
                    setTimeout(resolve, this.batchDelayMs)
                );
            }
        }
    }

    // List all Excel files in the blob container
    async listExcelFiles(
        containerName: string = "feedback-v2"
    ): Promise<string[]> {
        const containerClient =
            this.blobServiceClient.getContainerClient(containerName);
        const excelFiles: string[] = [];

        try {
            for await (const blob of containerClient.listBlobsFlat()) {
                if (
                    blob.name.toLowerCase().endsWith(".xlsx") ||
                    blob.name.toLowerCase().endsWith(".xls")
                ) {
                    excelFiles.push(blob.name);
                }
            }
        } catch (error) {
            console.error("Error listing blobs:", error);
            throw error;
        }

        return excelFiles;
    }

    // Import single Excel file from blob storage
    async importExcelFile(
        containerName: string,
        blobName: string
    ): Promise<void> {
        try {
            console.log(
                `Starting import of ${blobName} from container ${containerName}`
            );

            // Get blob client
            const containerClient =
                this.blobServiceClient.getContainerClient(containerName);

            // Find the latest version of the blob
            let latestVersion: string | undefined;
            let latestModified: Date | undefined;
            const allVersions: Array<{
                name: string;
                versionId?: string;
                lastModified?: Date;
            }> = [];

            for await (const blob of containerClient.listBlobsFlat({
                includeVersions: true,
            })) {
                if (blob.name === blobName && blob.properties.lastModified) {
                    allVersions.push({
                        name: blob.name,
                        versionId: blob.versionId,
                        lastModified: blob.properties.lastModified,
                    });

                    console.log(
                        "Found blob version:",
                        blob.name,
                        blob.versionId,
                        blob.properties.lastModified?.toISOString()
                    );

                    if (
                        !latestModified ||
                        blob.properties.lastModified > latestModified
                    ) {
                        latestModified = blob.properties.lastModified;
                        latestVersion = blob.versionId;
                        console.log("→ This is now the latest version");
                    }
                }
            }

            console.log(`Found ${allVersions.length} versions total`);
            console.log("Selected latest version:", latestVersion);
            console.log(
                "Selected latest modified:",
                latestModified?.toISOString()
            );

            // Use the latest version if found, otherwise use the default
            const blobClient = latestVersion
                ? containerClient
                      .getBlobClient(blobName)
                      .withVersion(latestVersion)
                : containerClient.getBlobClient(blobName);

            console.log("Using blob version:", latestVersion || "default");
            console.log("Blob URL:", blobClient.url);

            // Check if blob exists
            const exists = await blobClient.exists();
            if (!exists) {
                throw new Error(
                    `Blob ${blobName} not found in container ${containerName}`
                );
            }

            const properties = await blobClient.getProperties();
            console.log("Blob last modified:", properties.lastModified);

            // Download blob content
            const downloadResponse = await blobClient.download(0, undefined, {
                conditions: {},
                customerProvidedKey: undefined,
            });
            if (!downloadResponse.readableStreamBody) {
                throw new Error("Failed to download blob content");
            }

            // Convert stream to buffer
            const chunks: Buffer[] = [];
            for await (const chunk of downloadResponse.readableStreamBody) {
                chunks.push(Buffer.from(chunk));
            }
            const buffer = Buffer.concat(chunks);

            // Parse Excel file
            const workbook = XLSX.read(buffer, { type: "buffer" });
            console.log("Available sheets:", workbook.SheetNames);

            const sheetName = workbook.SheetNames[0]; // Use first sheet
            console.log("Using sheet:", sheetName);

            const worksheet = workbook.Sheets[sheetName];

            // Get worksheet range to see total rows
            const range = XLSX.utils.decode_range(worksheet["!ref"] || "A1");
            console.log(`Worksheet range: ${worksheet["!ref"]}`);
            console.log(
                `Total rows in sheet: ${range.e.r + 1} (including header)`
            );

            // Try with different parsing options
            const jsonData: ExcelFeedbackRow[] = XLSX.utils.sheet_to_json(
                worksheet,
                {
                    defval: "", // Use empty string for empty cells instead of undefined
                    blankrows: true, // Include blank rows
                    raw: false, // Convert values to strings
                }
            );

            console.log(
                `Found ${jsonData.length} rows in Excel file after parsing in blob`
            );

            // Show first few rows for debugging
            console.log(
                "First 3 rows:",
                JSON.stringify(jsonData.slice(0, 3), null, 2)
            );

            // Convert all rows to entities first (no skipping allowed)
            const entities: FeedbackTableEntity[] = [];

            for (let i = 0; i < jsonData.length; i++) {
                const row = jsonData[i];
                console.log(
                    `Processing row ${i + 2}:`,
                    JSON.stringify(row, null, 2)
                );

                try {
                    const entity = this.convertExcelRowToTableEntity(row);
                    entities.push(entity);
                    console.log(`✅ Row ${i + 2} converted successfully`);
                } catch (error) {
                    // Don't skip - throw error to stop processing
                    console.error(`❌ Row ${i + 2} failed:`, error);
                    throw new Error(
                        `Data validation failed at row ${i + 2}: ${
                            error instanceof Error
                                ? error.message
                                : "Unknown error"
                        }`
                    );
                }
            }

            console.log(
                `All ${entities.length} rows validated successfully. Starting batch upload...`
            );

            // Wait for user confirmation before uploading
            await this.waitForUserConfirmation(
                `Ready to upload ${entities.length} records to table storage.`
            );

            // Batch upload all entities
            await this.batchUploadEntities(entities);

            console.log(
                `Import completed: ${entities.length} records imported successfully`
            );
        } catch (error) {
            console.error("Error importing Excel file:", error);
            throw error;
        }
    }

    // Import multiple local Excel files
    async importLocalExcelFiles(directoryPath: string): Promise<void> {
        try {
            console.log(
                `Starting import from local directory: ${directoryPath}`
            );

            const fs = await import("fs");
            const path = await import("path");

            // Check if directory exists
            if (!fs.existsSync(directoryPath)) {
                throw new Error(`Directory not found: ${directoryPath}`);
            }

            // Read directory and filter Excel files
            const files = fs.readdirSync(directoryPath);
            const excelFiles = files.filter(
                (file) =>
                    file.toLowerCase().endsWith(".xlsx") ||
                    file.toLowerCase().endsWith(".xls")
            );

            if (excelFiles.length === 0) {
                console.log(
                    `No Excel files found in directory: ${directoryPath}`
                );
                return;
            }

            console.log(`Found ${excelFiles.length} Excel files in directory`);

            for (const fileName of excelFiles) {
                const fullPath = path.join(directoryPath, fileName);
                console.log(`\n--- Processing local file: ${fileName} ---`);

                // Read and process the file directly
                const buffer = fs.readFileSync(fullPath);
                console.log(`Reading local Excel file: ${fileName}`);

                // Parse Excel file
                const workbook = XLSX.read(buffer, { type: "buffer" });
                const sheetName = workbook.SheetNames[0]; // Use first sheet
                console.log("sheetName:", sheetName);
                const worksheet = workbook.Sheets[sheetName];
                const jsonData: ExcelFeedbackRow[] =
                    XLSX.utils.sheet_to_json(worksheet);

                console.log("Worksheet rows:", worksheet);

                // Show first few rows for debugging
                console.log(
                    "First 3 rows:",
                    JSON.stringify(jsonData.slice(0, 3), null, 2)
                );

                console.log(`Found ${jsonData.length} rows in Excel file`);

                // Convert all rows to entities first (no skipping allowed)
                const entities: FeedbackTableEntity[] = [];

                for (let i = 0; i < jsonData.length; i++) {
                    const row = jsonData[i];
                    console.log(
                        `Processing row ${i + 2}:`,
                        JSON.stringify(row, null, 2)
                    );

                    try {
                        const entity = this.convertExcelRowToTableEntity(row);
                        entities.push(entity);
                        console.log(`✅ Row ${i + 2} converted successfully`);
                    } catch (error) {
                        // Don't skip - throw error to stop processing
                        console.error(`❌ Row ${i + 2} failed:`, error);
                        throw new Error(
                            `Data validation failed at row ${i + 2}: ${
                                error instanceof Error
                                    ? error.message
                                    : "Unknown error"
                            }`
                        );
                    }
                }

                console.log(
                    `All ${entities.length} rows validated successfully for file ${fileName}. Starting batch upload...`
                );

                // Wait for user confirmation before uploading
                await this.waitForUserConfirmation(
                    `Ready to upload ${entities.length} records from ${fileName} to table storage.`
                );

                // Batch upload all entities
                await this.batchUploadEntities(entities);

                console.log(
                    `File ${fileName} completed: ${entities.length} records imported successfully`
                );
            }

            console.log(
                `\nAll ${excelFiles.length} local Excel files processed successfully!`
            );
        } catch (error) {
            console.error("Error importing local Excel files:", error);
            throw error;
        }
    }

    // Import all Excel files from the container
    async importAllExcelFiles(
        containerName: string = "feedback-v2"
    ): Promise<void> {
        const excelFiles = await this.listExcelFiles(containerName);
        console.log(
            `Found ${excelFiles.length} Excel files in container ${containerName}`
        );

        for (const fileName of excelFiles) {
            console.log(`\n--- Processing file: ${fileName} ---`);
            await this.importExcelFile(containerName, fileName);
        }

        console.log(
            `\nAll ${excelFiles.length} Excel files processed successfully!`
        );
    }
}

// Main execution function
async function main() {
    try {
        const args = process.argv.slice(2);
        const converter = new FeedbackConverter();

        if (args.length === 0) {
            // Default: Import from blob storage (feedback-v2 container)
            console.log(
                "Starting feedback data conversion from blob to table..."
            );
            await converter.importAllExcelFiles("feedback-v2");
            console.log("\nConversion completed successfully!");
        } else if (args[0] === "--local-dir" && args[1]) {
            // Import from local directory
            console.log(`Starting import from local directory: ${args[1]}`);
            await converter.importLocalExcelFiles(args[1]);
            console.log("\nLocal directory import completed successfully!");
        } else if (args[0] && !args[0].startsWith("--")) {
            // First argument is container name for blob storage
            const containerName = args[0];
            console.log(
                `Starting import from blob container: ${containerName}`
            );
            await converter.importAllExcelFiles(containerName);
            console.log("\nBlob container import completed successfully!");
        } else {
            // Show usage
            console.log("Usage:");
            console.log(
                "  node scripts/feedback-convert-from-blob-to-table.js                    # Import from default blob container (feedback-v2)"
            );
            console.log(
                "  node scripts/feedback-convert-from-blob-to-table.js <container-name>   # Import from specified blob container"
            );
            console.log(
                "  node scripts/feedback-convert-from-blob-to-table.js --local-dir <path> # Import from local directory"
            );
            console.log("");
            console.log("Examples:");
            console.log(
                "  node scripts/feedback-convert-from-blob-to-table.js"
            );
            console.log(
                "  node scripts/feedback-convert-from-blob-to-table.js feedback-v3"
            );
            console.log(
                "  node scripts/feedback-convert-from-blob-to-table.js --local-dir ./data/"
            );
            process.exit(1);
        }
    } catch (error) {
        console.error("Conversion failed:", error);
        process.exit(1);
    }
}

if (require.main === module) {
    main();
}
