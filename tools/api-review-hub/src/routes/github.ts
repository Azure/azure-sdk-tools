import { createHmac, timingSafeEqual } from "node:crypto";
import type { IncomingMessage, ServerResponse } from "node:http";

import { CosmosClient, type Container } from "@azure/cosmos";
import { DefaultAzureCredential } from "@azure/identity";
import { SecretClient, type KeyVaultSecret } from "@azure/keyvault-secrets";

import { getRequiredSetting } from "../config/settings.js";
import type { RepositoryRegistration } from "../models/models.js";
import { getRequiredHeader, logRequest, readRequestBody, sendEmpty, sendError } from "./http.js";

interface GitHubWebhookPayload {
    readonly action?: string;
    readonly installation?: {
        readonly id?: number;
    };
    readonly repository?: {
        readonly id?: number;
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

interface WebhookSecretKey {
    readonly name: string;
    readonly role: "current" | "previous";
}

interface WebhookSecretLookupResult extends WebhookSecretKey {
    readonly value?: string;
    readonly failureReason?: string;
    readonly enabled?: boolean;
    readonly expiresOn?: string;
    readonly notBefore?: string;
}

const credential = new DefaultAzureCredential();
const maxLoggedCommentBodyLength = 4000;
const repositoryRegistrationsContainerName = "repositoryRegistrations";
let cosmosClient: CosmosClient | undefined;
let repositoryRegistrationsContainer: Container | undefined;
let secretClient: SecretClient | undefined;

export async function handleGitHubWebhookEvent(request: IncomingMessage, response: ServerResponse): Promise<void> {
    const eventType = getRequiredHeader(request, "X-GitHub-Event");
    const deliveryId = getRequiredHeader(request, "X-GitHub-Delivery");
    const hookId = getRequiredHeader(request, "X-GitHub-Hook-ID");
    const hookInstallationTargetId = getRequiredHeader(request, "X-GitHub-Hook-Installation-Target-ID");
    const hookInstallationTargetType = getRequiredHeader(request, "X-GitHub-Hook-Installation-Target-Type");
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
    if (!hookId) {
        logRejectedWebhookDelivery("missingHeader", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 400, "missingHeader", "The GitHub hook ID header is required.", "X-GitHub-Hook-ID");
        return;
    }
    if (!hookInstallationTargetId) {
        logRejectedWebhookDelivery("missingHeader", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(
            response,
            400,
            "missingHeader",
            "The GitHub hook installation target ID header is required.",
            "X-GitHub-Hook-Installation-Target-ID",
        );
        return;
    }
    if (hookInstallationTargetType !== "repository") {
        logRejectedWebhookDelivery("unsupportedHookTarget", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 400, "unsupportedHookTarget", "The GitHub hook installation target must be a repository.");
        return;
    }
    if (payload.length === 0) {
        logRejectedWebhookDelivery("missingBody", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 400, "missingBody", "The GitHub webhook payload is required.");
        return;
    }

    const githubRepositoryId = parseGitHubId(hookInstallationTargetId);
    if (githubRepositoryId === undefined) {
        logRejectedWebhookDelivery("invalidHeader", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(
            response,
            400,
            "invalidHeader",
            "The GitHub hook installation target ID header must be a positive integer.",
            "X-GitHub-Hook-Installation-Target-ID",
        );
        return;
    }

    const githubWebhookId = parseGitHubId(hookId);
    if (githubWebhookId === undefined) {
        logRejectedWebhookDelivery("invalidHeader", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 400, "invalidHeader", "The GitHub hook ID header must be a positive integer.", "X-GitHub-Hook-ID");
        return;
    }

    const repositoryRegistration = await getRepositoryRegistration(githubRepositoryId);
    if (!repositoryRegistration) {
        logWebhookRegistrationError("unknownRepository", eventType, deliveryId, contentType, payload.length, githubRepositoryId, githubWebhookId);
        sendError(response, 401, "unknownRepository", "The GitHub repository is not registered for webhook processing.");
        return;
    }
    if (repositoryRegistration.status !== "active") {
        logWebhookRegistrationError("disabledRepository", eventType, deliveryId, contentType, payload.length, githubRepositoryId, githubWebhookId);
        sendError(response, 403, "disabledRepository", "The GitHub repository registration is not active.");
        return;
    }
    if (repositoryRegistration.githubWebhookId !== githubWebhookId) {
        logRejectedWebhookDelivery("unknownWebhook", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 401, "unknownWebhook", "The GitHub webhook is not registered for this repository.");
        return;
    }

    if (!(await isValidGitHubSignature(signatureSha256, payload, repositoryRegistration))) {
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
    if (decodedPayload.repository?.id !== repositoryRegistration.githubRepositoryId) {
        logRejectedWebhookDelivery("repositoryMismatch", eventType, deliveryId, signatureSha256, contentType, payload.length);
        sendError(response, 401, "repositoryMismatch", "The GitHub webhook payload repository does not match the registration.");
        return;
    }

    logRequest("POST /api/github/webhook-events", {
        eventType,
        deliveryId,
        hasSignatureSha256: true,
        contentType,
        payloadBytes: payload.length,
        hookId,
        hookInstallationTargetId,
        hookInstallationTargetType,
        action: decodedPayload.action,
        installationId: decodedPayload.installation?.id,
        repositoryId: decodedPayload.repository?.id,
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

function logWebhookRegistrationError(
    reason: string,
    eventType: string,
    deliveryId: string,
    contentType: string | undefined,
    payloadBytes: number,
    githubRepositoryId: number,
    githubWebhookId: number,
): void {
    console.error(
        JSON.stringify({
            endpoint: "POST /api/github/webhook-events rejected",
            reason,
            eventType,
            deliveryId,
            contentType,
            payloadBytes,
            githubRepositoryId,
            githubWebhookId,
        }),
    );
}

async function isValidGitHubSignature(
    signatureSha256: string,
    payload: Buffer,
    repositoryRegistration: RepositoryRegistration,
): Promise<boolean> {
    const expectedSignature = parseGitHubSha256Signature(signatureSha256);
    if (!expectedSignature) {
        return false;
    }

    const webhookSecretKeys = [
        { name: repositoryRegistration.webhookSecretKey, role: "current" },
        { name: repositoryRegistration.lastWebhookSecretKey, role: "previous" },
    ] satisfies WebhookSecretKey[];
    const failedSecretKeys: WebhookSecretLookupResult[] = [];

    for (const webhookSecretKey of webhookSecretKeys) {
        const secret = await getWebhookSecret(webhookSecretKey);
        if (!secret.value) {
            failedSecretKeys.push(secret);
            continue;
        }
        const actualSignature = createHmac("sha256", secret.value).update(payload).digest();

        if (actualSignature.length === expectedSignature.length && timingSafeEqual(actualSignature, expectedSignature)) {
            return true;
        }

        failedSecretKeys.push({ ...webhookSecretKey, failureReason: "signatureMismatch" });
    }

    console.error(
        JSON.stringify({
            endpoint: "POST /api/github/webhook-events rejected",
            reason: "invalidWebhookSecret",
            failedSecretKeys: failedSecretKeys.map(toWebhookSecretFailureLog),
        }),
    );

    return false;
}

function toWebhookSecretFailureLog(secret: WebhookSecretLookupResult): Record<string, unknown> {
    return {
        name: secret.name,
        role: secret.role,
        failureReason: secret.failureReason,
        enabled: secret.enabled,
        expiresOn: secret.expiresOn,
        notBefore: secret.notBefore,
    };
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

async function getWebhookSecret(webhookSecretKey: WebhookSecretKey): Promise<WebhookSecretLookupResult> {
    const keyVaultUri = await getRequiredSetting("keyvault_uri");
    secretClient ??= new SecretClient(keyVaultUri, credential);
    const secret = await getKeyVaultSecret(webhookSecretKey.name);

    if (!secret) {
        return { ...webhookSecretKey, failureReason: "notFound" };
    }

    const unusableReason = getWebhookSecretUnusableReason(secret);
    if (unusableReason) {
        return { ...webhookSecretKey, ...getWebhookSecretProperties(secret), failureReason: unusableReason };
    }

    if (!secret.value) {
        return { ...webhookSecretKey, ...getWebhookSecretProperties(secret), failureReason: "missingValue" };
    }

    return { ...webhookSecretKey, ...getWebhookSecretProperties(secret), value: secret.value };
}

function getWebhookSecretProperties(secret: KeyVaultSecret): Pick<WebhookSecretLookupResult, "enabled" | "expiresOn" | "notBefore"> {
    return {
        enabled: secret.properties.enabled,
        expiresOn: secret.properties.expiresOn?.toISOString(),
        notBefore: secret.properties.notBefore?.toISOString(),
    };
}

function getWebhookSecretUnusableReason(secret: KeyVaultSecret): string | undefined {
    const now = Date.now();

    if (secret.properties.enabled === false) {
        return "disabled";
    }
    if (secret.properties.expiresOn && secret.properties.expiresOn.getTime() <= now) {
        return "expired";
    }
    if (secret.properties.notBefore && secret.properties.notBefore.getTime() > now) {
        return "notBefore";
    }

    return undefined;
}

async function getKeyVaultSecret(webhookSecretKey: string): Promise<KeyVaultSecret | undefined> {
    try {
        return await secretClient!.getSecret(webhookSecretKey);
    } catch (error) {
        if (isNotFound(error)) {
            return undefined;
        }

        throw error;
    }
}

function isNotFound(error: unknown): boolean {
    return typeof error === "object" && error !== null && "statusCode" in error && error.statusCode === 404;
}

async function getRepositoryRegistration(githubRepositoryId: number): Promise<RepositoryRegistration | undefined> {
    const container = await getRepositoryRegistrationsContainer();
    const query = {
        query: "SELECT TOP 2 * FROM c WHERE c.githubRepositoryId = @githubRepositoryId",
        parameters: [{ name: "@githubRepositoryId", value: githubRepositoryId }],
    };
    const { resources } = await container.items.query<RepositoryRegistration>(query).fetchAll();

    if (resources.length > 1) {
        throw new Error(`Found multiple repository registrations for GitHub repository ID ${githubRepositoryId}.`);
    }

    return resources[0];
}

async function getRepositoryRegistrationsContainer(): Promise<Container> {
    if (repositoryRegistrationsContainer) {
        return repositoryRegistrationsContainer;
    }

    const cosmosEndpoint = await getRequiredSetting("cosmos_endpoint");
    const cosmosDatabaseName = await getRequiredSetting("cosmos_db_name");
    cosmosClient ??= new CosmosClient({ endpoint: cosmosEndpoint, aadCredentials: credential });
    repositoryRegistrationsContainer = cosmosClient.database(cosmosDatabaseName).container(repositoryRegistrationsContainerName);
    return repositoryRegistrationsContainer;
}

function parseGitHubId(value: string): number | undefined {
    if (!/^\d+$/.test(value)) {
        return undefined;
    }

    const parsed = Number(value);
    return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
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