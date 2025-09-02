import {
    HttpHandler,
    HttpRequest,
    HttpResponseInit,
    InvocationContext,
} from "@azure/functions";

// API Response interface
interface AnalyticsApiResponseBody<TData> {
    success: boolean;
    data?: TData[];
    error?: string;
}

export abstract class AnalyticsServiceBase<TEntity, TData>
{
    // Azure Function: Get Feedback Data
    public handler: HttpHandler = async (
        request: HttpRequest,
        context: InvocationContext
    ): Promise<HttpResponseInit> => {
        context.log(
            "AnalyticsServiceBase Handler function processed a request."
        );

        try {
            // Handle different HTTP methods
            switch (request.method) {
                case "GET": {
                    const entities = await this.getEntities();
                    const data = this.deserializeData(entities);
                    const response: AnalyticsApiResponseBody<TData> = {
                        success: true,
                        data,
                    };
                    return { status: 200, jsonBody: response };
                }
                default: {
                    const response: AnalyticsApiResponseBody<TData> = {
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
    };

    protected abstract getEntities(): Promise<TEntity[]>;
    protected abstract deserializeData(entities: TEntity[]): TData[];
}
