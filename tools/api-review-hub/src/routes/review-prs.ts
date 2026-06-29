import type { IncomingMessage, ServerResponse } from "node:http";

import type { OperationUpdate, ReviewPullRequestCreationRequest } from "../models/models.js";
import { acceptOperationUpdate, acceptReviewPullRequestCreation, getOperation } from "../services/review-pr-service.js";
import { getString, isRecord, logRequest, readJsonBody, sendError, sendJson } from "./http.js";

export async function handleRequestReviewPullRequestCreation(request: IncomingMessage, response: ServerResponse): Promise<void> {
    let body: unknown;
    try {
        body = await readJsonBody(request);
    } catch {
        sendError(response, 400, "invalidJson", "The request body must be valid JSON.");
        return;
    }

    logRequest("POST /api/review-prs", {
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

export async function handleGetOperationStatus(
    request: IncomingMessage,
    response: ServerResponse,
    _url: URL,
    pathMatch: RegExpMatchArray,
): Promise<void> {
    const operationId = pathMatch[1] ?? "";
    logRequest("GET /api/operations/{operationId}", {
        operationId,
    });

    const operation = getOperation(operationId);

    if (!operation) {
        sendError(response, 404, "operationNotFound", "The operation was not found.", "operationId");
        return;
    }

    sendJson(response, 200, operation);
}

export async function handleAcceptOperationUpdate(
    request: IncomingMessage,
    response: ServerResponse,
    _url: URL,
    pathMatch: RegExpMatchArray,
): Promise<void> {
    let body: unknown;
    try {
        body = await readJsonBody(request);
    } catch {
        sendError(response, 400, "invalidJson", "The request body must be valid JSON.");
        return;
    }

    const operationId = pathMatch[1] ?? "";
    logRequest("POST /api/operations/{operationId}", {
        operationId,
        body,
    });

    const validationError = validateOperationUpdate(operationId, body);
    if (validationError) {
        sendError(response, 400, "invalidRequest", validationError.message, validationError.target);
        return;
    }

    const operation = acceptOperationUpdate(operationId, body as OperationUpdate);
    if (!operation) {
        sendError(response, 404, "operationNotFound", "The operation was not found.", "operationId");
        return;
    }

    sendJson(response, 202, operation);
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

function validateOperationUpdate(operationId: string, value: unknown): { message: string; target: string } | undefined {
    if (!isRecord(value)) {
        return { message: "The request body must be a JSON object.", target: "body" };
    }

    if (value.operationId !== operationId) {
        return { message: "The operationId field must match the operationId path parameter.", target: "operationId" };
    }

    for (const field of ["mode", "language", "buildId", "project", "result"] as const) {
        if (!getString(value[field])) {
            return { message: `The ${field} field is required.`, target: field };
        }
    }

    if (!isRecord(value.artifacts)) {
        return { message: "The artifacts field is required.", target: "artifacts" };
    }

    for (const field of ["result"] as const) {
        if (!getString(value.artifacts[field])) {
            return { message: `The artifacts.${field} field is required.`, target: `artifacts.${field}` };
        }
    }

    return undefined;
}

export type ValidReviewPullRequestCreationRequest = ReviewPullRequestCreationRequest;