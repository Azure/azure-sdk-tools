import { Buffer } from "node:buffer";
import type { IncomingMessage, ServerResponse } from "node:http";

import type { ErrorResponse } from "../models/models.js";

export function sendJson(response: ServerResponse, statusCode: number, body: unknown): void {
    response.writeHead(statusCode, { "content-type": "application/json" });
    response.end(JSON.stringify(body));
}

export function sendEmpty(response: ServerResponse, statusCode: number): void {
    response.writeHead(statusCode);
    response.end();
}

export function sendError(response: ServerResponse, statusCode: number, code: string, message: string, target?: string): void {
    const body: ErrorResponse = { error: { code, message, target } };
    sendJson(response, statusCode, body);
}

export async function readRequestBody(request: IncomingMessage): Promise<Buffer> {
    const chunks: Buffer[] = [];

    for await (const chunk of request) {
        chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
    }

    return Buffer.concat(chunks);
}

export async function readJsonBody(request: IncomingMessage): Promise<unknown> {
    const body = await readRequestBody(request);

    if (body.length === 0) {
        return undefined;
    }

    return JSON.parse(body.toString("utf8"));
}

export function getRequiredHeader(request: IncomingMessage, headerName: string): string | undefined {
    const value = request.headers[headerName.toLowerCase()];

    if (Array.isArray(value)) {
        return value[0];
    }

    return value;
}

export function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function getString(value: unknown): string | undefined {
    return typeof value === "string" && value.trim().length > 0 ? value : undefined;
}

export function logRequest(endpoint: string, details: Record<string, unknown>): void {
    console.log(JSON.stringify({ endpoint, ...details }));
}