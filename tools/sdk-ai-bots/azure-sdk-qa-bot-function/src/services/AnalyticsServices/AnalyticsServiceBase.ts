import { TableEntity } from "@azure/data-tables";
import { TableService } from "../StorageService";
import {
    HttpRequest,
    HttpResponseInit,
    InvocationContext,
} from "@azure/functions";

// API Response interface
interface AnalyticsApiResponse<TData> {
    success: boolean;
    data?: TData[];
    error?: string;
}

export abstract class AnalyticsServiceBase<TEntity extends TableEntity, TData> {
    private tableService: TableService;

    constructor(tableName: string) {
        this.tableService = new TableService(tableName);
    }

    // Azure Function: Get Feedback Data
    public async getData(
        request: HttpRequest,
        context: InvocationContext
    ): Promise<HttpResponseInit> {
        context.log(
            "AnalyticsServiceBase Handler function processed a request."
        );

        try {
            // Handle different HTTP methods
            switch (request.method) {
                case "GET": {
                    const data = await this.getAllData();
                    const response: AnalyticsApiResponse<TData> = {
                        success: true,
                        data,
                    };
                    return { status: 200, jsonBody: response };
                }
                default: {
                    const response: AnalyticsApiResponse<TData> = {
                        success: false,
                        error: "Method not allowed",
                    };
                    return { status: 405, jsonBody: response };
                }
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
                },
            };
        }
    }

    private async getAllData(): Promise<TData[]> {
        const entities = await this.tableService.queryEntities<TEntity>({});
        return entities.map((entity) => this.deserializeData(entity));
    }

    protected abstract deserializeData(entity: TEntity): TData;
}
