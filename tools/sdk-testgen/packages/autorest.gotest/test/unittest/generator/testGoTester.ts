import * as path from 'path';
import * as process from 'process';
import { ExtensionName, TestCodeModel, TestCodeModeler } from '@autorest/testmodeler/dist/src/core/model';
import { GoTestGenerator, processRequest } from '../../../src/generator/goTester';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { MockTool } from '../../tools';
import { TestConfig } from '@autorest/testmodeler/dist/src/common/testConfig';

describe('processRequest of go-tester', () => {
    let spyGenerateMockTest;
    let spyGenRenderData;
    let spyGenerateExample;
    let spyGenerateScenarioTest;
    beforeEach(() => {
        Helper.outputToModelerfour = jest.fn().mockResolvedValue(undefined);
        Helper.dump = jest.fn().mockResolvedValue(undefined);
        Helper.addCodeModelDump = jest.fn().mockResolvedValue(undefined);
        spyGenerateMockTest = jest.spyOn(GoTestGenerator.prototype, 'generateMockTest').mockResolvedValue(undefined);
        spyGenRenderData = jest.spyOn(GoTestGenerator.prototype, 'genRenderData').mockReturnValue(undefined);
        spyGenerateExample = jest.spyOn(GoTestGenerator.prototype, 'generateExample').mockReturnValue(undefined);
        spyGenerateScenarioTest = jest.spyOn(GoTestGenerator.prototype, 'generateScenarioTest').mockReturnValue(undefined);
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('processRequest with export-codemodel', async () => {
        TestCodeModeler.getSessionFromHost = jest.fn().mockResolvedValue({
            getValue: jest.fn().mockImplementation((key: string) => {
                if (key === '') {
                    return {
                        testmodeler: {
                            'export-codemodel': true,
                        },
                    };
                } else if (key === 'header-text') {
                    return '';
                }
            }),
        });

        await processRequest(undefined);

        expect(spyGenRenderData).toHaveBeenCalledTimes(1);
        expect(spyGenerateMockTest).toHaveBeenCalledTimes(1);
        expect(spyGenerateExample).not.toHaveBeenCalled();
        expect(Helper.outputToModelerfour).toHaveBeenCalledTimes(1);
        expect(Helper.addCodeModelDump).toHaveBeenCalledTimes(2);
        expect(Helper.dump).toHaveBeenCalledTimes(1);
    });

    it('processRequest without export-codemodel', async () => {
        TestCodeModeler.getSessionFromHost = jest.fn().mockResolvedValue({
            getValue: jest.fn().mockImplementation((key: string) => {
                if (key === '') {
                    return {
                        testmodeler: {
                            'export-codemodel': false,
                        },
                    };
                } else if (key === 'header-text') {
                    return '';
                }
            }),
        });
        await processRequest(undefined);

        expect(spyGenRenderData).toHaveBeenCalledTimes(1);
        expect(spyGenerateMockTest).toHaveBeenCalledTimes(1);
        expect(spyGenerateExample).not.toHaveBeenCalled();
        expect(spyGenerateScenarioTest).not.toHaveBeenCalled();
        expect(Helper.outputToModelerfour).toHaveBeenCalledTimes(1);
        expect(Helper.addCodeModelDump).not.toHaveBeenCalled();
        expect(Helper.dump).toHaveBeenCalledTimes(1);
    });

    it("don't generate mock test if generate-mock-test is false", async () => {
        TestCodeModeler.getSessionFromHost = jest.fn().mockResolvedValue({
            getValue: jest.fn().mockImplementation((key: string) => {
                if (key === '') {
                    return {
                        testmodeler: {
                            'generate-mock-test': false,
                        },
                    };
                } else if (key === 'header-text') {
                    return '';
                }
            }),
        });
        await processRequest(undefined);
        expect(spyGenerateMockTest).not.toHaveBeenCalled();
    });

    it('generate sdk example if generate-sdk-example is true', async () => {
        TestCodeModeler.getSessionFromHost = jest.fn().mockResolvedValue({
            getValue: jest.fn().mockImplementation((key: string) => {
                if (key === '') {
                    return {
                        testmodeler: {
                            'generate-sdk-example': true,
                        },
                    };
                } else if (key === 'header-text') {
                    return '';
                }
            }),
        });
        await processRequest(undefined);
        expect(spyGenerateExample).toHaveBeenCalledTimes(1);
    });

    it('generate scenario test if generate-scenario-test is true', async () => {
        TestCodeModeler.getSessionFromHost = jest.fn().mockResolvedValue({
            getValue: jest.fn().mockImplementation((key: string) => {
                if (key === '') {
                    return {
                        testmodeler: {
                            'generate-scenario-test': true,
                        },
                    };
                } else if (key === 'header-text') {
                    return '';
                }
            }),
        });
        await processRequest(undefined);
        expect(spyGenerateScenarioTest).toHaveBeenCalledTimes(1);
    });
});

describe('GoTestGenerator from RP agrifood', () => {
    let testCodeModel: TestCodeModeler;
    beforeAll(async () => {
        const codeModel = MockTool.createCodeModel();
        testCodeModel = TestCodeModeler.createInstance(codeModel as TestCodeModel, {
            testmodeler: {
                'split-parents-value': true,
            },
        });
        testCodeModel.genMockTests();
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('Generate MockTest and SDK example', async () => {
        const outputs = {};
        const spyGenerate = jest.spyOn(GoTestGenerator.prototype, 'writeToHost').mockImplementation((filename, output) => {
            outputs[filename] = output;
        });

        const generator = await new GoTestGenerator(undefined, testCodeModel.codeModel, new TestConfig({}));
        generator.genRenderData();
        await generator.generateMockTest('mockTest.go.njk');
        generator.generateExample('exampleTest.go.njk');
        await generator.generateScenarioTest('scenarioTest.go.njk');

        let exampleFiles = 0;
        for (const group of testCodeModel.codeModel.operationGroups) {
            for (const op of group.operations) {
                if (Object.keys(op.extensions?.[ExtensionName.xMsExamples]).length > 0) {
                    exampleFiles += 1;
                    break;
                }
            }
        }

        expect(spyGenerate).toHaveBeenCalledTimes(1 /* mock test */ + exampleFiles + testCodeModel.codeModel.testModel.scenarioTests.length);
        expect(outputs).toMatchSnapshot();
    });
});

describe('GoTestGenerator from RP signalR', () => {
    let testCodeModel: TestCodeModeler;
    beforeAll(async () => {
        const codeModel = MockTool.loadCodeModel('signalR/test-modeler-pre.yaml');
        const swaggerFolder = path.join(__dirname, '..', '..', 'swagger/specification/signalr/resource-manager/');
        testCodeModel = TestCodeModeler.createInstance(codeModel as TestCodeModel, {
            __parents: {
                'Microsoft.SignalRService/preview/2020-07-01-preview/signalr.json': process.platform.toLowerCase().startsWith('win')
                    ? `file:///${swaggerFolder}`
                    : `file://${swaggerFolder}`,
            },
            'input-file': ['Microsoft.SignalRService/preview/2020-07-01-preview/signalr.json'],
            'test-resources': [
                {
                    test: 'Microsoft.SignalRService/preview/2020-07-01-preview/test-scenarios/signalR.yaml',
                },
            ],
            testmodeler: {
                'split-parents-value': true,
            },
        });
        testCodeModel.genMockTests();
        await testCodeModel.loadTestResources();
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('Generate scenario test', async () => {
        const outputs = {};
        const spyGenerate = jest.spyOn(GoTestGenerator.prototype, 'writeToHost').mockImplementation((filename, output) => {
            outputs[filename] = output;
        });

        const generator = await new GoTestGenerator(undefined, testCodeModel.codeModel, new TestConfig({}));
        generator.genRenderData();
        await generator.generateScenarioTest('scenarioTest.go.njk');

        expect(spyGenerate).toHaveBeenCalledTimes(testCodeModel.codeModel.testModel.scenarioTests.length);
        expect(outputs).toMatchSnapshot();
    });
});
