const defaultEndpoint = "https://api-review-hub-staging.azurewebsites.net";
const defaultMessage = "Test issue from API Review Hub GitHub App";
const endpoint = (process.env.API_REVIEW_HUB_ENDPOINT ?? defaultEndpoint).replace(/\/$/, "");
const message = process.env.TEST_ISSUE_MESSAGE || process.argv.slice(2).join(" ") || defaultMessage;

async function main(): Promise<void> {
    const response = await fetch(`${endpoint}/api/test-issue`, {
        method: "POST",
        headers: {
            "content-type": "application/json",
        },
        body: JSON.stringify({ message }),
    });

    const responseText = await response.text();
    if (!response.ok) {
        throw new Error(`Request failed with status ${response.status}: ${responseText}`);
    }

    const result = responseText ? (JSON.parse(responseText) as unknown) : {};
    console.log(JSON.stringify(result, null, 2));
}

main().catch((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`Failed to create test issue: ${message}`);
    process.exitCode = 1;
});