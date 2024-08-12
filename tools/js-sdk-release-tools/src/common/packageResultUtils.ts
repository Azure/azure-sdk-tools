import { Changelog } from '../changelog/changelogGenerator';
import { ChangelogResult, NpmPackageInfo, PackageResult } from './types';

export function initPackageResult(): PackageResult {
    const breakingChangeItems = [];
    const hasBreakingChange = false;
    const content = '';
    const changelogInfo: ChangelogResult = { content, hasBreakingChange, breakingChangeItems };
    const packageInfo: PackageResult = {
        packageName: '',
        version: '',
        language: 'JavaScript',
        path: ['rush.json', 'common/config/rush/pnpm-lock.yaml'],
        apiViewArtifact: [],
        packageFolder: '',
        typespecProject: [],
        artifacts: [],
        changelog: changelogInfo,
        result: 'failed'
    };
    return packageInfo;
}

export function updateChangelogResult(packageResult: PackageResult, changelog: Changelog | undefined): void {
    packageResult.changelog.breakingChangeItems = changelog?.getBreakingChangeItems() ?? [];
    packageResult.changelog.content = changelog?.displayChangeLog() ?? '';
    packageResult.changelog.hasBreakingChange = changelog?.hasBreakingChange ?? false;
}

// TODO: need a instruction
export function updateInstructionResult(packageResult: PackageResult, instruction: string): void {}

export function updateNpmPackageResult(
    packageResult: PackageResult,
    npmPackageInfo: NpmPackageInfo,
    typeSpecDirectory: string,
    generatedPackageDirectory: string
): void {
    packageResult.packageName = npmPackageInfo.name;
    packageResult.version = npmPackageInfo.version;
    packageResult.typespecProject = [typeSpecDirectory];
    packageResult.packageFolder = generatedPackageDirectory;
    packageResult.path.push(generatedPackageDirectory);
}
