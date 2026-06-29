export interface ErrorResponse {
    readonly error: {
        readonly code: string;
        readonly message: string;
        readonly target?: string;
    };
}

export interface GitBranchReference {
    readonly owner: string;
    readonly repo: string;
    readonly name: string;
}

export interface ReviewPullRequestCreationRequest {
    readonly language: string;
    readonly packageName: string;
    readonly baseTag: string;
    readonly targetBranch: GitBranchReference;
}

export interface ReviewPullRequestCreationAcceptedResponse {
    readonly operationId: string;
    readonly status: "accepted";
}

export interface OperationStatus {
    readonly operationId: string;
    readonly status: "accepted" | "running" | "succeeded" | "failed";
    readonly reviewPullRequest?: unknown;
    readonly failureReason?: string;
    readonly log?: string;
}

export interface OperationArtifactNames {
    readonly base?: string;
    readonly target?: string;
    readonly result: string;
}

export interface OperationUpdate {
    readonly operationId: string;
    readonly mode: "create" | "update";
    readonly language: string;
    readonly buildId: string;
    readonly project: string;
    readonly result: "Succeeded" | "SucceededWithIssues" | "Failed" | "Canceled" | "Skipped";
    readonly artifacts: OperationArtifactNames;
}

export interface ReleaseGateDecision {
    readonly allowed: boolean;
    readonly reason: "approved" | "missingApproval" | "rejected" | "unknownPackage" | "staleArtifact";
    readonly approval?: unknown;
}

export interface MarkPackageVersionReleasedRequest {
    readonly language: string;
    readonly packageName: string;
    readonly version: string;
    readonly releasedOn: string;
}