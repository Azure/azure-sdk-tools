import { randomUUID } from "node:crypto";

import type {
    ReviewPullRequestCreationAcceptedResponse,
    ReviewPullRequestCreationOperationStatus,
    ReviewPullRequestCreationRequest,
} from "../models/models.js";

const operations = new Map<string, ReviewPullRequestCreationOperationStatus>();

export function acceptReviewPullRequestCreation(
    _request: ReviewPullRequestCreationRequest,
): ReviewPullRequestCreationAcceptedResponse {
    const operationId = randomUUID();
    operations.set(operationId, { operationId, status: "accepted" });

    return { operationId, status: "accepted" };
}

export function getReviewPullRequestCreationOperation(operationId: string): ReviewPullRequestCreationOperationStatus | undefined {
    return operations.get(operationId);
}