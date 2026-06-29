import type { IncomingMessage, ServerResponse } from "node:http";

import type { MarkPackageVersionReleasedRequest } from "../models/models.js";
import { evaluateReleaseGate, markPackageVersionReleased } from "../services/release-service.js";
import { getString, isRecord, logRequest, readJsonBody, sendEmpty, sendError, sendJson } from "./http.js";

export async function handleEvaluateReleaseGate(request: IncomingMessage, response: ServerResponse, url: URL): Promise<void> {
    const query = url.searchParams;
    const language = query.get("language") ?? "";
    const packageName = query.get("packageName") ?? "";
    const version = query.get("version") ?? "";
    const apiHash = query.get("apiHash") ?? "";

    logRequest("GET /api/releases/check-gate", {
        query: { language, packageName, version, apiHash },
    });

    for (const [field, value] of Object.entries({ language, packageName, version, apiHash })) {
        if (!value) {
            sendError(response, 400, "missingQueryParameter", `The ${field} query parameter is required.`, field);
            return;
        }
    }

    sendJson(response, 200, evaluateReleaseGate({ language, packageName, version, apiHash }));
}

export async function handleMarkPackageVersionReleased(request: IncomingMessage, response: ServerResponse): Promise<void> {
    let body: unknown;
    try {
        body = await readJsonBody(request);
    } catch {
        sendError(response, 400, "invalidJson", "The request body must be valid JSON.");
        return;
    }

    logRequest("POST /api/releases/mark-released", {
        body,
    });

    const validationError = validateMarkReleasedRequest(body);
    if (validationError) {
        sendError(response, 400, "invalidRequest", validationError.message, validationError.target);
        return;
    }

    markPackageVersionReleased(body as MarkPackageVersionReleasedRequest);
    sendEmpty(response, 204);
}

function validateMarkReleasedRequest(value: unknown): { message: string; target: string } | undefined {
    if (!isRecord(value)) {
        return { message: "The request body must be a JSON object.", target: "body" };
    }

    for (const field of ["language", "packageName", "version", "releasedOn"] as const) {
        if (!getString(value[field])) {
            return { message: `The ${field} field is required.`, target: field };
        }
    }

    return undefined;
}