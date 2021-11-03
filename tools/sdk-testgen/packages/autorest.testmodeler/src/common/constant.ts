export enum Config {
    exportCodemodel = 'testmodeler.export-codemodel',
    scenarioVariableDefaults = 'testmodeler.scenario.variable-defaults',
    parents = '__parents',
    inputFile = 'input-file',
    testResources = 'test-resources',
    test = 'test',
    splitParentsValue = 'testmodeler.split-parents-value',
    disabledExamples = 'testmodeler.mock.disabled-examples',
}

export enum TestScenarioVariableNames {
    subscriptionId = 'subscriptionId',
    location = 'location',
    resourceGroupName = 'resourceGroupName',
}

export const variableDefaults = {
    [TestScenarioVariableNames.subscriptionId]: '00000000-00000000-00000000-00000000',
    [TestScenarioVariableNames.location]: 'westus',
    [TestScenarioVariableNames.resourceGroupName]: 'scenarioTestTempGroup',
};

export enum OavStepType {
    restCall = 'restCall',
    armTemplate = 'armTemplateDeployment',
    rawCall = 'rawCall',
}
