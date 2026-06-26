import type { IncomingMessage, ServerResponse } from "node:http";

import type { ReviewPullRequestCreationRequest } from "../models/models.js";
import { acceptReviewPullRequestCreation, getReviewPullRequestCreationOperation } from "../services/review-pr-service.js";
import { getRequiredHeader, getString, isRecord, logRequest, readJsonBody, sendError, sendJson } from "./http.js";

export async function handleRequestReviewPullRequestCreation(request: IncomingMessage, response: ServerResponse): Promise<void> {
    const authorization = getRequiredHeader(request, "Authorization");
    if (!authorization) {
        sendError(response, 401, "missingAuthorization", "The Authorization header is required.", "Authorization");
        return;
    }

    let body: unknown;
    try {
        body = await readJsonBody(request);
    } catch {
        sendError(response, 400, "invalidJson", "The request body must be valid JSON.");
        return;
    }

    logRequest("POST /api/review-prs", {
        hasAuthorization: Boolean(authorization),
        body,
    });

    const validationError = validateReviewPullRequestCreationRequest(body);
    if (validationError) {
        sendError(response, 400, "invalidRequest", validationError.message, validationError.target);
        return;
    }

    const operation = acceptReviewPullRequestCreation(body as ReviewPullRequestCreationRequest);
    sendJson(response, 202, operation);
}

export async function handleGetReviewPullRequestCreationOperationStatus(
    request: IncomingMessage,
    response: ServerResponse,
    _url: URL,
    pathMatch: RegExpMatchArray,
): Promise<void> {
    const authorization = getRequiredHeader(request, "Authorization");
    if (!authorization) {
        sendError(response, 401, "missingAuthorization", "The Authorization header is required.", "Authorization");
        return;
    }

    const operationId = pathMatch[1] ?? "";
    logRequest("GET /api/review-prs/operations/{operationId}", {
        hasAuthorization: Boolean(authorization),
        operationId,
    });

    const operation = getReviewPullRequestCreationOperation(operationId);

    if (!operation) {
        sendError(response, 404, "operationNotFound", "The review pull request creation operation was not found.", "operationId");
        return;
    }

    sendJson(response, 200, operation);
}

function validateReviewPullRequestCreationRequest(value: unknown): { message: string; target: string } | undefined {
    if (!isRecord(value)) {
        return { message: "The request body must be a JSON object.", target: "body" };
    }

    for (const field of ["language", "packageName", "baseTag"] as const) {
        if (!getString(value[field])) {
            return { message: `The ${field} field is required.`, target: field };
        }
    }

    if (!isRecord(value.targetBranch)) {
        return { message: "The targetBranch field is required.", target: "targetBranch" };
    }

    for (const field of ["owner", "repo", "name"] as const) {
        if (!getString(value.targetBranch[field])) {
            return { message: `The targetBranch.${field} field is required.`, target: `targetBranch.${field}` };
        }
    }

    return undefined;
}

export type ValidReviewPullRequestCreationRequest = ReviewPullRequestCreationRequest;