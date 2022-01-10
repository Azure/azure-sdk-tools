import { configDefaults as baseConfigDefaults } from '@autorest/testmodeler/dist/src/common/constant';

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
    sendExampleId = 'testmodeler.mock.send-example-id',
    verifyResponse = 'testmodeler.mock.verify-response',
}

export const configDefaults = {
    ...baseConfigDefaults,
    [Config.exportCodemodel]: false,
    [Config.generateMockTest]: true,
    [Config.generateSdkExample]: false,
    [Config.generateScenarioTest]: false,
    [Config.filePrefix]: 'zz_generated_',
    [Config.exampleFilePrefix]: 'ze_generated_',
    [Config.testFilePrefix]: 'zt_generated_',
};
