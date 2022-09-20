export enum ORG {
    Azure = 'Azure',
    Ms = 'microsoft',
}

export enum SDK {
    GoSDK = 'go',
    NetSDK = 'net',
    JsSDK = 'js',
    JavaSDK = 'java',
    PythonSDK = 'python',
}

export enum RepoType {
    Github = 'github',
    DevOps = 'devops',
}

export enum AzureSDKTaskName {
    Init = 'init',
    GenerateAndBuild = 'generateAndBuild',
    MockTest = 'mockTest',
    LiveTest = 'liveTest',
}

export enum ServiceType {
    DataPlane = 'data-plane',
    ResourceManager = 'resource-manager',
}

export enum StorageType {
    Blob = 'blob',
    Db = 'db',
    EventHub = 'eventhub',
}

export type SDKPipelineStatus = 'bot_update' | 'queued' | 'in_progress' | 'completed' | 'skipped';
