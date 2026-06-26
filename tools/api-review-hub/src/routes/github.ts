import type { IncomingMessage, ServerResponse } from "node:http";

import { getRequiredHeader, logRequest, readRequestBody, sendEmpty, sendError } from "./http.js";

export async function handleGitHubWebhookEvent(request: IncomingMessage, response: ServerResponse): Promise<void> {
    const eventType = getRequiredHeader(request, "X-GitHub-Event");
    const deliveryId = getRequiredHeader(request, "X-GitHub-Delivery");
    const signatureSha256 = getRequiredHeader(request, "X-Hub-Signature-256");
    const payload = await readRequestBody(request);

    logRequest("POST /api/github/webhook-events", {
        eventType,
        deliveryId,
        hasSignatureSha256: Boolean(signatureSha256),
        payloadBytes: payload.length,
    });

    if (!eventType) {
        sendError(response, 400, "missingHeader", "The GitHub event header is required.", "X-GitHub-Event");
        return;
    }
    if (!deliveryId) {
        sendError(response, 400, "missingHeader", "The GitHub delivery header is required.", "X-GitHub-Delivery");
        return;
    }
    if (!signatureSha256) {
        sendError(response, 400, "missingHeader", "The GitHub signature header is required.", "X-Hub-Signature-256");
        return;
    }
    if (payload.length === 0) {
        sendError(response, 400, "missingBody", "The GitHub webhook payload is required.");
        return;
    }

    sendEmpty(response, 202);
}