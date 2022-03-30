export enum Config {
    exportCodemodel = 'testmodeler.export-codemodel',
    scenarioVariableDefaults = 'testmodeler.scenario.variable-defaults',
    parents = '__parents',
    inputFile = 'input-file',
    testResources = 'test-resources',
    test = 'test',
    splitParentsValue = 'testmodeler.split-parents-value',
    disabledExamples = 'testmodeler.mock.disabled-examples',
    sendExampleId = 'testmodeler.mock.send-example-id',
    verifyResponse = 'testmodeler.mock.verify-response',
    scenarioCodeModelRestCallOnly = 'testmodeler.scenario.codemodel-restcall-only',
    useExampleModel = 'testmodeler.use-example-model',
    addArmTemplatePayloadString = 'testmodeler.add-armtemplate-payload-string',
}

export const configDefaults = {
    [Config.exportCodemodel]: false,
    [Config.testResources]: [],
    [Config.splitParentsValue]: false,
    [Config.sendExampleId]: true,
    [Config.verifyResponse]: true,
    [Config.scenarioCodeModelRestCallOnly]: true,
    [Config.useExampleModel]: true,
    [Config.addArmTemplatePayloadString]: false,
};

export enum TestScenarioVariableNames {
    subscriptionId = 'subscriptionId',
    location = 'location',
    resourceGroupName = 'resourceGroupName',
}

export const testScenarioVariableDefault = {
    [TestScenarioVariableNames.subscriptionId]: '00000000-00000000-00000000-00000000',
    [TestScenarioVariableNames.location]: 'westus',
    [TestScenarioVariableNames.resourceGroupName]: 'scenarioTestTempGroup',
};

export enum OavStepType {
    restCall = 'restCall',
    armTemplate = 'armTemplateDeployment',
    rawCall = 'rawCall',
}
