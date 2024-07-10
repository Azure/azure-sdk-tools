export enum SDKType {
    HighLevelClient = 'HighLevelClient',
    RestLevelClient = 'RestLevelClient',
    ModularClient = 'ModularClient',
};

export enum ApiVersionType {
    None = 'None',
    Stable = 'Stable',
    Preview = 'Preview',
}

export interface ChangelogInfo {
    content: string;
    hasBreakingChange: boolean;
}

export interface GeneratedPackageInfo {
    packageName: string;
    version: string;
    path: string[];
    changelog: ChangelogInfo;
    artifacts: string[];
    result: "succeeded" | "failed";
    packageFolder?: string;
}

export interface GenerationOutputInfo {
    packages: GeneratedPackageInfo[];
}

export interface ModularClientGenerationOptions {
    tspProjectPath: string;
    gitCommitId: string;
    skipGeneration: boolean;
    swaggerRepoUrl: string;
}
