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
    Init = "init",
    GenerateAndBuild = "generateAndBuild",
    MockTest = "mockTest",
    LiveTest = "liveTest",
}

export enum ServiceType {
    DataPlane = "data-plane",
    ResourceManager = "resource-manager",
}