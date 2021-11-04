export enum Config {
    exportCodemodel = 'testmodeler.export-codemodel',
    generateMockTest = 'testmodeler.generate-mock-test',
    generateSdkExample = 'testmodeler.generate-sdk-example',
    generateScenarioTest = 'testmodeler.generate-scenario-test',
    parents = '__parents',
    outputFolder = 'output-folder',
    module = 'module',
    filePrefix = 'file-prefix',
    exampleFilePrefix = 'example-file-prefix',
    testFilePrefix = 'test-file-prefix',
}

export const variableDefaults = {
    [Config.exampleFilePrefix]: 'ze_generated_',
    [Config.testFilePrefix]: 'zt_generated_',
};

export enum TargetMode {
    mockTest = 'MOCK-TEST',
    scenarioTest = 'SCENARIO-TEST',
    sample = 'SAMPLE',
}
