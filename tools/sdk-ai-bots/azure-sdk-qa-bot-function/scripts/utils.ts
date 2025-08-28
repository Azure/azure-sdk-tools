import { TableEntity } from "@azure/data-tables";
import * as XLSX from "xlsx";
import * as readline from "readline";
import path from "path";
import fs from "fs";
import { TableService } from "../src/services/StorageService";
import { ChannelConfigService } from "../src/services/AnalyticsServices/ChannelConfigService";
import { HttpRequest, InvocationContext } from "@azure/functions";

export class ExcelHandler {
    public loadExcel<TRow>(buffer: any) {
        // Parse Excel file
        const workbook = XLSX.read(buffer, { type: "buffer" });
        const sheetName = workbook.SheetNames[0]; // Use first sheet
        console.log("sheetName:", sheetName);
        const worksheet = workbook.Sheets[sheetName];
        const rows: TRow[] = XLSX.utils.sheet_to_json(worksheet);
        console.log(`Found ${rows.length} rows in Excel file`);
        return rows;
    }
}

export class Converter {
    protected tableService: TableService;
    protected excelHandler = new ExcelHandler();

    constructor(tableName: string) {
        this.tableService = new TableService(tableName);
    }

    /**
     * Waits for user confirmation before proceeding
     */
    protected async waitForUserConfirmation(message: string): Promise<void> {
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

    public async convert<TEntity extends TableEntity, TRow>(
        readFilePromise: Promise<any[]>,
        convertFunc: (row: TRow) => TEntity
    ): Promise<void> {
        try {
            const buffers = await readFilePromise;
            const entities = (
                await Promise.all(
                    buffers.map(async (buffer) => {
                        const rows = this.excelHandler.loadExcel<TRow>(buffer);

                        // Convert all rows to entities first (no skipping allowed)
                        const currentEntities: TEntity[] = [];

                        for (let i = 0; i < rows.length; i++) {
                            const row = rows[i];
                            console.log(
                                `Processing row ${i + 2}:`,
                                JSON.stringify(row, null, 2)
                            );

                            try {
                                const entity = convertFunc(row);
                                currentEntities.push(entity);
                                console.log("Table entity:", entity);
                                console.log(
                                    `✅ Row ${i + 2} converted successfully`
                                );
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

                        return currentEntities;
                    })
                )
            ).flat();

            // Wait for user confirmation before uploading
            await this.waitForUserConfirmation(
                `Ready to upload ${entities.length} records to table storage.`
            );

            // Batch upload all entities
            await this.tableService.batchUploadEntities(entities);

            console.log(`\nAll files processed successfully!`);
        } catch (error) {
            console.error("Error importing local Excel files:", error);
            throw error;
        }
    }

    public async readLocalExcelFiles(
        directoryPath: string
    ): Promise<NonSharedBuffer[]> {
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
            console.log(`No Excel files found in directory: ${directoryPath}`);
            return;
        }

        console.log(`Found ${excelFiles.length} Excel files in directory`);

        const buffers: NonSharedBuffer[] = excelFiles.map((fileName) => {
            const fullPath = path.join(directoryPath, fileName);
            console.log(`\n--- Processing local file: ${fileName} ---`);

            // Read and process the file directly
            const buffer = fs.readFileSync(fullPath);
            console.log(`Reading local Excel file: ${fileName}`);
            return buffer;
        });
        return buffers;
    }
}

export async function loadChannelMapping(): Promise<Map<string, string>> {
    const channelConfigHandler = new ChannelConfigService().handler;
    const channelMappingResponse = await channelConfigHandler(
        new HttpRequest({ method: "GET", url: "http://none" }),
        new InvocationContext({})
    );
    const channels = (channelMappingResponse as any).jsonBody.data;
    const idNameMap = (channels as { id: string; name: string }[]).reduce(
        (map, channel) => {
            map.set(channel.id, channel.name);
            return map;
        },
        new Map<string, string>()
    );
    return idNameMap;
}
