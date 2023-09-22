import {NPMViewResult, StringMap, tr} from "@ts-common/azure-js-dev-tools";
import {logger} from "./logger";
const semverInc = require('semver/functions/inc')

export function getLatestStableVersion(npmViewResult: NPMViewResult) {
    const distTags: StringMap<string> | undefined = npmViewResult['dist-tags'];
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
        logger.logError(`Invalid stableVersion ${stableVersion}`);
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
