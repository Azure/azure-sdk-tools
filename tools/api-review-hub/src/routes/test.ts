import type { IncomingMessage, ServerResponse } from "node:http";

import type { TestIssueRequest, TestIssueResponse } from "../models/models.js";
import { createGitHubIssue } from "../services/test-issue-service.js";
import { getString, isRecord, logRequest, readJsonBody, sendError, sendJson } from "./http.js";

export async function handleTestIssue(request: IncomingMessage, response: ServerResponse): Promise<void> {
    let body: unknown;
    try {
        body = await readJsonBody(request);
    } catch {
        sendError(response, 400, "invalidJson", "The request body must be valid JSON.");
        return;
    }

    const validationError = validateTestIssueRequest(body);
    if (validationError) {
        sendError(response, 400, "invalidRequest", validationError.message, validationError.target);
        return;
    }

    const requestBody = body as TestIssueRequest;
    logRequest("POST /api/test-issue", { message: requestBody.message });

    let issue;
    try {
        issue = await createGitHubIssue(requestBody.message);
    } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        console.error(`Failed to create test GitHub issue: ${message}`);
        sendError(response, 500, "githubIssueCreationFailed", "The service could not create the GitHub issue.");
        return;
    }

    const responseBody: TestIssueResponse = {
        status: "created",
        message: requestBody.message,
        issueNumber: issue.number,
        issueUrl: issue.url,
    };
    sendJson(response, 201, responseBody);
}

function validateTestIssueRequest(value: unknown): { message: string; target: string } | undefined {
    if (!isRecord(value)) {
        return { message: "The request body must be a JSON object.", target: "body" };
    }

    if (!getString(value.message)) {
        return { message: "The message field is required.", target: "message" };
    }

    return undefined;
}