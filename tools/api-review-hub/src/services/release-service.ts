import type { MarkPackageVersionReleasedRequest, ReleaseGateDecision } from "../models/models.js";

interface ReleaseGateRequest {
    readonly language: string;
    readonly packageName: string;
    readonly version: string;
    readonly apiHash: string;
}

const releasedPackageVersions = new Set<string>();

export function evaluateReleaseGate(_request: ReleaseGateRequest): ReleaseGateDecision {
    return {
        allowed: false,
        reason: "missingApproval",
    };
}

export function markPackageVersionReleased(request: MarkPackageVersionReleasedRequest): void {
    releasedPackageVersions.add(`${request.language}:${request.packageName}:${request.version}`);
}