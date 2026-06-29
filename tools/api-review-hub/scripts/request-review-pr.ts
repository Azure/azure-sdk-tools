import type {
    OperationStatus,
    ReviewPullRequestCreationAcceptedResponse,
    ReviewPullRequestCreationRequest,
} from "../src/models/models.js";

const endpoint = (process.env.API_REVIEW_HUB_ENDPOINT ?? "https://api-review-hub-staging.azurewebsites.net").replace(/\/$/, "");
const authorization = process.env.API_REVIEW_HUB_AUTHORIZATION ?? "Bearer local-test";
const pollIntervalMs = Number(process.env.API_REVIEW_HUB_POLL_INTERVAL_MS ?? 5_000);

const request: ReviewPullRequestCreationRequest = {
    language: "python",
    packageName: "azure-keyvault-keys",
    baseTag: "azure-keyvault-keys_4.10.0",
    targetBranch: {
        owner: "tjprescott",
        repo: "azure-sdk-for-python",
        name: "main",
    },
};

async function main(): Promise<void> {
    console.log(`Requesting API review PR from ${endpoint}`);
    console.log(JSON.stringify(request, null, 2));

    const accepted = await postJson<ReviewPullRequestCreationAcceptedResponse>(`${endpoint}/api/review-prs`, request);
    console.log("Accepted operation:");
    console.log(JSON.stringify(accepted, null, 2));

    while (true) {
        await delay(pollIntervalMs);

        const status = await getJson<OperationStatus>(`${endpoint}/api/operations/${accepted.operationId}`);
        console.log(JSON.stringify(status, null, 2));

        if (status.status === "succeeded" || status.status === "failed") {
            process.exitCode = status.status === "succeeded" ? 0 : 1;
            return;
        }
    }
}

async function postJson<T>(url: string, body: unknown): Promise<T> {
    const response = await fetch(url, {
        method: "POST",
        headers: {
            authorization,
            "content-type": "application/json",
        },
        body: JSON.stringify(body),
    });

    return readResponse<T>(response);
}

async function getJson<T>(url: string): Promise<T> {
    const response = await fetch(url, {
        headers: { authorization },
    });

    return readResponse<T>(response);
}

async function readResponse<T>(response: Response): Promise<T> {
    const text = await response.text();
    const body = text ? (JSON.parse(text) as unknown) : undefined;

    if (!response.ok) {
        throw new Error(`Request failed with status ${response.status}: ${JSON.stringify(body)}`);
    }

    return body as T;
}

function delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

main().catch((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`Failed to request API review PR: ${message}`);
    process.exitCode = 1;
});