import { createHmac, timingSafeEqual } from "node:crypto";
import type { IncomingMessage, ServerResponse } from "node:http";

import { DefaultAzureCredential } from "@azure/identity";
import { SecretClient } from "@azure/keyvault-secrets";

import { getRequiredSetting } from "../config/settings.js";
import { getRequiredHeader, logRequest, readRequestBody, sendEmpty, sendError } from "./http.js";

interface GitHubWebhookPayload {
    readonly action?: string;
    readonly repository?: {
        readonly full_name?: string;
    };
    readonly sender?: {
        readonly login?: string;
    };
    readonly pull_request?: {
        readonly number?: number;
    };
    readonly issue?: {
        readonly number?: number;
    };
    readonly comment?: {
        readonly id?: number;
        readonly html_url?: string;
        readonly body?: string;
        readonly user?: {
            readonly login?: string;
        };
    };
    readonly ref?: string;
}

const credential = new DefaultAzureCredential();
const maxLoggedCommentBodyLength = 4000;
const webhookSecretName = "github-webhook-secret";
let secretClient: SecretClient | undefined;
let webhookSecret: string | undefined;

export async function handleGitHubWebhookEvent(request: IncomingMessage, response: ServerResponse): Promise<void> {
    const eventType = getRequiredHeader(request, "X-GitHub-Event");
    const deliveryId = getRequiredHeader(request, "X-GitHub-Delivery");
    const signatureSha256 = getRequiredHeader(request, "X-Hub-Signature-256");
    const contentType = getRequiredHeader(request, "Content-Type");
    const payload = await readRequestBody(request);

    if (!eventType) {
        logRejectedWebhookDelivery("missingHeader", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 400, "missingHeader", "The GitHub event header is required.", "X-GitHub-Event");
        return;
    }
    if (!deliveryId) {
        logRejectedWebhookDelivery("missingHeader", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 400, "missingHeader", "The GitHub delivery header is required.", "X-GitHub-Delivery");
        return;
    }
    if (!signatureSha256) {
        logRejectedWebhookDelivery("missingHeader", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 400, "missingHeader", "The GitHub signature header is required.", "X-Hub-Signature-256");
        return;
    }
    if (payload.length === 0) {
        logRejectedWebhookDelivery("missingBody", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 400, "missingBody", "The GitHub webhook payload is required.");
        return;
    }

    if (!(await isValidGitHubSignature(signatureSha256, payload))) {
        logRejectedWebhookDelivery("invalidSignature", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 401, "invalidSignature", "The GitHub webhook signature is invalid.", "X-Hub-Signature-256");
        return;
    }

    const decodedPayload = decodeGitHubWebhookPayload(payload, contentType);
    if (!decodedPayload) {
        logRejectedWebhookDelivery("invalidBody", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 400, "invalidBody", "The GitHub webhook payload must be valid JSON.");
        return;
    }

    logRequest("POST /api/github/webhook-events", {
        eventType,
        deliveryId,
        hasSignatureSha256: true,
        contentType,
        payloadBytes: payload.length,
        action: decodedPayload.action,
        repository: decodedPayload.repository?.full_name,
        sender: decodedPayload.sender?.login,
        pullRequestNumber: decodedPayload.pull_request?.number,
        issueNumber: decodedPayload.issue?.number,
        commentId: decodedPayload.comment?.id,
        commentUrl: decodedPayload.comment?.html_url,
        commentAuthor: decodedPayload.comment?.user?.login,
        commentBody: truncateLogValue(decodedPayload.comment?.body),
        ref: decodedPayload.ref,
    });

    sendEmpty(response, 202);
}

function logRejectedWebhookDelivery(
    reason: string,
    eventType: string | undefined,
    deliveryId: string | undefined,
    signatureSha256: string | undefined,
    contentType: string | undefined,
    payloadBytes: number,
): void {
    logRequest("POST /api/github/webhook-events rejected", {
        reason,
        eventType,
        deliveryId,
        hasSignatureSha256: Boolean(signatureSha256),
        contentType,
        payloadBytes,
    });
}

async function isValidGitHubSignature(signatureSha256: string, payload: Buffer): Promise<boolean> {
    const expectedSignature = parseGitHubSha256Signature(signatureSha256);
    if (!expectedSignature) {
        return false;
    }

    const secret = await getWebhookSecret();
    const actualSignature = createHmac("sha256", secret).update(payload).digest();

    return actualSignature.length === expectedSignature.length && timingSafeEqual(actualSignature, expectedSignature);
}

function parseGitHubSha256Signature(signatureSha256: string): Buffer | undefined {
    const prefix = "sha256=";
    if (!signatureSha256.startsWith(prefix)) {
        return undefined;
    }

    const signatureHex = signatureSha256.slice(prefix.length);
    if (!/^[0-9a-f]{64}$/i.test(signatureHex)) {
        return undefined;
    }

    return Buffer.from(signatureHex, "hex");
}

async function getWebhookSecret(): Promise<string> {
    if (webhookSecret) {
        return webhookSecret;
    }

    const keyVaultUri = await getRequiredSetting("keyvault_uri");
    secretClient ??= new SecretClient(keyVaultUri, credential);
    const secret = await secretClient.getSecret(webhookSecretName);

    if (!secret.value) {
        throw new Error(`Missing Key Vault secret value: ${webhookSecretName}`);
    }

    webhookSecret = secret.value;
    return webhookSecret;
}

function decodeGitHubWebhookPayload(payload: Buffer, contentType: string | undefined): GitHubWebhookPayload | undefined {
    const payloadText = payload.toString("utf8");

    try {
        if (contentType?.toLowerCase().startsWith("application/x-www-form-urlencoded")) {
            const formPayload = new URLSearchParams(payloadText).get("payload");
            return formPayload ? parseGitHubWebhookPayloadJson(formPayload) : undefined;
        }

        return parseGitHubWebhookPayloadJson(payloadText);
    } catch {
        return undefined;
    }
}

function parseGitHubWebhookPayloadJson(payloadJson: string): GitHubWebhookPayload | undefined {
    const value = JSON.parse(payloadJson);
    return typeof value === "object" && value !== null && !Array.isArray(value) ? (value as GitHubWebhookPayload) : undefined;
}

function truncateLogValue(value: string | undefined): string | undefined {
    if (!value || value.length <= maxLoggedCommentBodyLength) {
        return value;
    }

    return `${value.slice(0, maxLoggedCommentBodyLength)}...`;
}