import type { IncomingMessage, ServerResponse } from "node:http";

import { handleGitHubWebhookEvent } from "./github.js";
import { handleGetReviewPullRequestCreationOperationStatus, handleRequestReviewPullRequestCreation } from "./review-prs.js";
import { handleEvaluateReleaseGate, handleMarkPackageVersionReleased } from "./releases.js";
import { sendError } from "./http.js";
import { handleTestIssue } from "./test.js";

type RouteHandler = (request: IncomingMessage, response: ServerResponse, url: URL, pathMatch: RegExpMatchArray) => Promise<void>;

interface Route {
    readonly method: string;
    readonly pattern: RegExp;
    readonly handler: RouteHandler;
}

const requiredRoutes: readonly Route[] = [
    { method: "POST", pattern: /^\/api\/github\/webhook-events$/, handler: handleGitHubWebhookEvent },
    { method: "POST", pattern: /^\/api\/review-prs$/, handler: handleRequestReviewPullRequestCreation },
    {
        method: "GET",
        pattern: /^\/api\/review-prs\/operations\/([^/]+)$/,
        handler: handleGetReviewPullRequestCreationOperationStatus,
    },
    { method: "GET", pattern: /^\/api\/releases\/check-gate$/, handler: handleEvaluateReleaseGate },
    { method: "POST", pattern: /^\/api\/releases\/mark-released$/, handler: handleMarkPackageVersionReleased },
    { method: "POST", pattern: /^\/api\/test-issue$/, handler: handleTestIssue },
];

export interface Router {
    handle(request: IncomingMessage, response: ServerResponse): Promise<void>;
}

export function createRouter(): Router {
    return {
        async handle(request: IncomingMessage, response: ServerResponse): Promise<void> {
            const method = request.method ?? "GET";
            const url = new URL(request.url ?? "/", "http://localhost");

            for (const route of requiredRoutes) {
                const pathMatch = url.pathname.match(route.pattern);

                if (pathMatch && route.method === method) {
                    await route.handler(request, response, url, pathMatch);
                    return;
                }
            }

            if (requiredRoutes.some((route) => route.pattern.test(url.pathname))) {
                sendError(response, 405, "methodNotAllowed", "The requested endpoint does not support this HTTP method.");
                return;
            }

            sendError(response, 404, "notFound", "The requested endpoint was not found.");
        },
    };
}