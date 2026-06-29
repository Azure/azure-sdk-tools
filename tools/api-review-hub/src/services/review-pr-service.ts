import { randomUUID } from "node:crypto";

import type {
    OperationStatus,
    OperationUpdate,
    ReviewPullRequestCreationAcceptedResponse,
    ReviewPullRequestCreationRequest,
} from "../models/models.js";

export interface ReviewPullRequestCreationResult {
    readonly reviewPullRequest: Record<string, unknown>;
    readonly log?: string;
}

export interface ReviewPullRequestCreationOptions {
    readonly operationId?: string;
    readonly onLog?: (message: string) => void;
}

const operations = new Map<string, OperationStatus>();

export function acceptReviewPullRequestCreation(
    _request: ReviewPullRequestCreationRequest,
): ReviewPullRequestCreationAcceptedResponse {
    const operationId = randomUUID();
    operations.set(operationId, { operationId, status: "accepted" });

    return { operationId, status: "accepted" };
}

export function getOperation(operationId: string): OperationStatus | undefined {
    return operations.get(operationId);
}

export function acceptOperationUpdate(operationId: string, update: OperationUpdate): OperationStatus | undefined {
    const operation = operations.get(operationId);
    if (!operation) {
        return undefined;
    }

    const status = update.result === "Succeeded" || update.result === "SucceededWithIssues" ? "succeeded" : "failed";
    const updatedOperation: OperationStatus = {
        ...operation,
        operationId,
        status,
        failureReason: status === "failed" ? `Artifact generation completed with result ${update.result}.` : operation.failureReason,
    };
    operations.set(operationId, updatedOperation);
    return updatedOperation;
}

export async function createReviewPullRequest(
    _request: ReviewPullRequestCreationRequest,
    options: ReviewPullRequestCreationOptions = {},
): Promise<ReviewPullRequestCreationResult> {
    const operationId = options.operationId ?? randomUUID();

    return {
        reviewPullRequest: {
            operationId,
            status: "notImplemented",
        },
        log: "Review PR creation is not implemented yet.",
    };
}
