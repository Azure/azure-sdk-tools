import {logger} from "./logger.js";
import {inc as semverInc} from "semver";
import { ApiVersionType } from "../common/types.js"

function getDistTags(npmViewResult: Record<string, unknown>): Record<string, string> | undefined {
    const distTags = npmViewResult['dist-tags'];
    if (!isStringStringRecord(distTags)) {
        logger.warn(`Failed to get expected dist-tags record.`);
        return undefined;
    }
    return distTags;
}

function isStringStringRecord(record: unknown): record is Record<string, string> {
    return record !== undefined &&
        record !== null &&
        typeof record === 'object' &&
        !Array.isArray(record) && 
        Object.entries(record).every(
            ([k, v]) => typeof k === "string" && typeof v === "string"
        );
}

export function getUsedVersions(npmViewResult: Record<string, unknown>): string[] {
    const versions = npmViewResult['versions'];
    if (!isStringStringRecord(versions)) return [];
    return Object.keys(versions);    
}

// TODO: consider back compatibility
// NOTE: latest tag will only contains stable version,
//       so if the package is not GA, we need to get latest version from beta tag for back compatibility.
export function getLatestVersion(npmViewResult: Record<string, unknown>) {
    const distTags = getDistTags(npmViewResult);
    if (!distTags) return undefined;
    const latestVersion = distTags['latest'];
    const betaVersion = distTags['beta'];
    if (latestVersion) {
        return latestVersion;
    } else if (betaVersion) {
        return betaVersion;
    } else {
        logger.warn(`Failed to find latest or beta version found in dist-tags.`);
        return undefined;
    }
}

export function getBetaVersion(npmViewResult: Record<string, unknown>) {
    const distTags = getDistTags(npmViewResult);
    // TODO
}

export function getVersion(npmViewResult: Record<string, unknown>, tag: string): string | undefined {
    const distTags = getDistTags(npmViewResult);
    return distTags ? distTags[tag] : undefined;
}

export function getversionDate(npmViewResult: Record<string, unknown>, version : string) {
    const time = npmViewResult['time'];
    if (!isStringStringRecord(time)) {
        logger.error(`Failed to get expected time record.`);
        return undefined;
    }
    return time[version];
}

export function getLatestStableVersion(npmViewResult: Record<string, any> | undefined) {
    const distTags: Record<string, any> | undefined = npmViewResult?.['dist-tags'];
    const stableVersion = distTags && distTags['latest'];
    return stableVersion;
}
export function isBetaVersion(stableVersion: string) {
    return stableVersion.includes('beta');
}

export function bumpMajorVersion(version: string, usedVersions: string[] | undefined) {
    let newVersion = semverInc(version, 'major', 'beta');
    while (usedVersions && usedVersions.includes(newVersion)) {
        newVersion = semverInc(newVersion, 'major', 'beta');
    }
    return newVersion;
}

export function bumpMinorVersion(version: string, usedVersions: string[] | undefined) {
    let newVersion = semverInc(version, 'minor', 'beta');
    while (usedVersions && usedVersions.includes(newVersion)) {
        newVersion = semverInc(newVersion, 'minor', 'beta');
    }
    return newVersion;
}

export function bumpPatchVersion(version: string, usedVersions: string[] | undefined) {
    let newVersion = semverInc(version, 'patch', 'beta');
    while (usedVersions && usedVersions.includes(newVersion)) {
        newVersion = semverInc(newVersion, 'patch', 'beta');
    }
    return newVersion;
}

export function bumpPreviewVersion(version: string, usedVersions: string[] | undefined) {
    let newVersion = semverInc(version, 'pre', 'beta');
    if (newVersion.endsWith('beta.0')) {
        // we should start from beta.1
        return bumpPreviewVersion(newVersion, usedVersions);
    }
    while (usedVersions && usedVersions.includes(newVersion)) {
        newVersion = semverInc(newVersion, 'pre', 'beta');
    }
    return newVersion;
}

export function getNewVersion(stableVersion: string | undefined, usedVersions: string[] | undefined, hasBreakingChange, isStableRelease: boolean): string {
    if (!stableVersion) {
        logger.error(`Invalid stableVersion '${stableVersion}'.`);
        process.exit(1);
    }
    if (isStableRelease) {
        if (hasBreakingChange) {
            return bumpMajorVersion(stableVersion, usedVersions);
        } else {
            return bumpMinorVersion(stableVersion, usedVersions);
        }
    } else {
        if (isBetaVersion(stableVersion)) {
            return bumpPreviewVersion(stableVersion, usedVersions);
        } else {
            if (hasBreakingChange) {
                return bumpPreviewVersion(bumpMajorVersion(stableVersion, usedVersions), usedVersions);
            } else {
                return bumpPreviewVersion(bumpMinorVersion(stableVersion, usedVersions), usedVersions);
            }
        }
    }
}

export async function isStableSDKReleaseType(apiVersionType: string, options: { apiVersion: string | undefined, sdkReleaseType: string | undefined }) {
    let isStableRelease = apiVersionType != ApiVersionType.Preview;
    if(options.apiVersion && options.sdkReleaseType ) {
        logger.info(`Detected appVersion is ${options.apiVersion}, sdkReleaseType is ${options.sdkReleaseType}`);
        isStableRelease = options.sdkReleaseType == 'stable'
    }
    return isStableRelease;
}