export enum SDKType {
    HighLevelClient = 'HighLevelClient',
    RestLevelClient = 'RestLevelClient',
    ModularClient = 'ModularClient'
}

export enum ApiVersionType {
    None = 'None',
    Stable = 'Stable',
    Preview = 'Preview'
}

export interface ChangelogInfo {
    content: string;
    hasBreakingChange: boolean;
    breakingChangeItems: string[];
}

export interface GeneratedPackageInfo {
    packageName: string;
    version: string;
    path: string[];
    changelog: ChangelogInfo;
    artifacts: string[];
    result: 'succeeded' | 'failed';
    packageFolder?: string;
}

export interface GenerationOutputInfo {
    packages: GeneratedPackageInfo[];
}

export interface ModularClientPackageOptions {
    typeSpecDirectory: string;
    gitCommitId: string;
    skip: boolean;
    repoUrl: string;
    versionPolicyName: 'management' | 'client';
}

export interface NpmPackageInfo {
    name: string;
    version: string;
}
