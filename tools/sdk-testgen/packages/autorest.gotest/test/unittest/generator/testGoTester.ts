import * as path from 'path';
import * as process from 'process';
import { BaseCodeGenerator } from '../../../src/generator/baseGenerator';
import { ExampleCodeGenerator, ExampleDataRender } from '../../../src/generator/exampleGenerator';
import { ExtensionName, TestCodeModel, TestCodeModeler } from '@autorest/testmodeler/dist/src/core/model';
import { GenerateContext } from '../../../src/generator/generateContext';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { MockTestCodeGenerator, MockTestDataRender } from '../../../src/generator/mockTestGenerator';
import { MockTool } from '../../tools';
import { SampleCodeGenerator, SampleDataRender } from '../../../src/generator/sampleGenerator';
import { ScenarioTestCodeGenerator, ScenarioTestDataRender } from '../../../src/generator/scenarioTestGenerator';
import { TestConfig } from '@autorest/testmodeler/dist/src/common/testConfig';
import { configDefaults } from '../../../src/common/constant';
import { processRequest } from '../../../src/generator/goTester';

describe('processRequest of go-tester', () => {
    let spyExampleRenderData;
    let spyExampleGenerateCode;
    let spyMockTestRenderData;
    let spyMockTestGenerateCode;
    let spyScenarioTestRenderData;
    let spyScenarioTestGenerateCode;
    let spySampleRenderData;
    let spySampleGenerateCode;

    beforeEach(() => {
        Helper.outputToModelerfour = jest.fn().mockResolvedValue(undefined);
        Helper.dump = jest.fn().mockResolvedValue(undefined);
        Helper.addCodeModelDump = jest.fn().mockResolvedValue(undefined);
        spyExampleRenderData = jest.spyOn(ExampleDataRender.prototype, 'renderData').mockReturnValue(undefined);
        spyExampleGenerateCode = jest.spyOn(ExampleCodeGenerator.prototype, 'generateCode').mockReturnValue(undefined);
        spyMockTestRenderData = jest.spyOn(MockTestDataRender.prototype, 'renderData').mockReturnValue(undefined);
        spyMockTestGenerateCode = jest.spyOn(MockTestCodeGenerator.prototype, 'generateCode').mockReturnValue(undefined);
        spyScenarioTestRenderData = jest.spyOn(ScenarioTestDataRender.prototype, 'renderData').mockReturnValue(undefined);
        spyScenarioTestGenerateCode = jest.spyOn(ScenarioTestCodeGenerator.prototype, 'generateCode').mockReturnValue(undefined);
        spySampleRenderData = jest.spyOn(SampleDataRender.prototype, 'renderData').mockReturnValue(undefined);
        spySampleGenerateCode = jest.spyOn(SampleCodeGenerator.prototype, 'generateCode').mockReturnValue(undefined);
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
                            'generate-mock-test': true,
                            'export-codemodel': true,
                        },
                    };
                } else if (key === 'header-text') {
                    return '';
                }
            }),
        });

        await processRequest(undefined);

        expect(spyMockTestRenderData).toHaveBeenCalledTimes(1);
        expect(spyMockTestGenerateCode).toHaveBeenCalledTimes(1);
        expect(spyExampleRenderData).not.toHaveBeenCalled();
        expect(spyExampleGenerateCode).not.toHaveBeenCalled();
        expect(spyScenarioTestRenderData).not.toHaveBeenCalled();
        expect(spyScenarioTestGenerateCode).not.toHaveBeenCalled();
        expect(spySampleRenderData).not.toHaveBeenCalled();
        expect(spySampleGenerateCode).not.toHaveBeenCalled();
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
                            'generate-mock-test': true,
                            'export-codemodel': false,
                        },
                    };
                } else if (key === 'header-text') {
                    return '';
                }
            }),
        });
        await processRequest(undefined);

        expect(spyMockTestRenderData).toHaveBeenCalledTimes(1);
        expect(spyMockTestGenerateCode).toHaveBeenCalledTimes(1);
        expect(spyExampleRenderData).not.toHaveBeenCalled();
        expect(spyExampleGenerateCode).not.toHaveBeenCalled();
        expect(spyScenarioTestRenderData).not.toHaveBeenCalled();
        expect(spyScenarioTestGenerateCode).not.toHaveBeenCalled();
        expect(spySampleRenderData).not.toHaveBeenCalled();
        expect(spySampleGenerateCode).not.toHaveBeenCalled();
        expect(Helper.outputToModelerfour).toHaveBeenCalledTimes(1);
        expect(Helper.addCodeModelDump).not.toHaveBeenCalled();
        expect(Helper.dump).toHaveBeenCalledTimes(1);
    });

    it("don't generate mock test if generate-mock-test is true", async () => {
        TestCodeModeler.getSessionFromHost = jest.fn().mockResolvedValue({
            getValue: jest.fn().mockImplementation((key: string) => {
                if (key === '') {
                    return {
                        testmodeler: {
                            'generate-mock-test': true,
                        },
                    };
                } else if (key === 'header-text') {
                    return '';
                }
            }),
        });
        await processRequest(undefined);
        expect(spyMockTestRenderData).toHaveBeenCalledTimes(1);
        expect(spyMockTestGenerateCode).toHaveBeenCalledTimes(1);
        expect(spyExampleRenderData).not.toHaveBeenCalled();
        expect(spyExampleGenerateCode).not.toHaveBeenCalled();
        expect(spyScenarioTestRenderData).not.toHaveBeenCalled();
        expect(spyScenarioTestGenerateCode).not.toHaveBeenCalled();
        expect(spySampleRenderData).not.toHaveBeenCalled();
        expect(spySampleGenerateCode).not.toHaveBeenCalled();
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
        expect(spyMockTestRenderData).toHaveBeenCalledTimes(1);
        expect(spyMockTestGenerateCode).not.toHaveBeenCalled();
        expect(spyExampleRenderData).toHaveBeenCalledTimes(1);
        expect(spyExampleGenerateCode).toHaveBeenCalledTimes(1);
        expect(spyScenarioTestRenderData).not.toHaveBeenCalled();
        expect(spyScenarioTestGenerateCode).not.toHaveBeenCalled();
        expect(spySampleRenderData).not.toHaveBeenCalled();
        expect(spySampleGenerateCode).not.toHaveBeenCalled();
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
        expect(spyMockTestRenderData).toHaveBeenCalledTimes(1);
        expect(spyMockTestGenerateCode).not.toHaveBeenCalled();
        expect(spyExampleRenderData).not.toHaveBeenCalled();
        expect(spyExampleGenerateCode).not.toHaveBeenCalled();
        expect(spyScenarioTestRenderData).toHaveBeenCalledTimes(1);
        expect(spyScenarioTestGenerateCode).toHaveBeenCalledTimes(1);
        expect(spySampleRenderData).not.toHaveBeenCalled();
        expect(spySampleGenerateCode).not.toHaveBeenCalled();
    });

    it('generate sdk sample if generate-sdk-sample is true', async () => {
        TestCodeModeler.getSessionFromHost = jest.fn().mockResolvedValue({
            getValue: jest.fn().mockImplementation((key: string) => {
                if (key === '') {
                    return {
                        testmodeler: {
                            'generate-sdk-sample': true,
                        },
                    };
                } else if (key === 'header-text') {
                    return '';
                }
            }),
        });
        await processRequest(undefined);
        expect(spyMockTestRenderData).toHaveBeenCalledTimes(1);
        expect(spyMockTestGenerateCode).not.toHaveBeenCalled();
        expect(spyExampleRenderData).not.toHaveBeenCalled();
        expect(spyExampleGenerateCode).not.toHaveBeenCalled();
        expect(spyScenarioTestRenderData).not.toHaveBeenCalled();
        expect(spyScenarioTestGenerateCode).not.toHaveBeenCalled();
        expect(spySampleRenderData).toHaveBeenCalledTimes(1);
        expect(spySampleGenerateCode).toHaveBeenCalledTimes(1);
    });
});

describe('GoTestGenerator from RP agrifood', () => {
    let testCodeModel: TestCodeModeler;
    beforeAll(async () => {
        const codeModel = MockTool.createCodeModel();
        testCodeModel = TestCodeModeler.createInstance(
            codeModel as TestCodeModel,
            new TestConfig(
                {
                    testmodeler: {
                        'split-parents-value': true,
                    },
                },
                configDefaults,
            ),
        );
        testCodeModel.genMockTests(undefined);
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('Generate MockTest and SDK example', async () => {
        const outputs = {};
        const spyCodeGenerate = jest.spyOn(BaseCodeGenerator.prototype as any, 'writeToHost').mockImplementation((filename: string, output: string) => {
            outputs[filename] = output;
        });

        const context = new GenerateContext(undefined, testCodeModel.codeModel, new TestConfig({}, configDefaults));
        const mockTestDataRender = new MockTestDataRender(context);
        mockTestDataRender.renderData();
        const mockTestCodeGenerator = new MockTestCodeGenerator(context);
        mockTestCodeGenerator.generateCode({});
        const exampleDataRender = new ExampleDataRender(context);
        exampleDataRender.renderData();
        const exampleCodeGenerator = new ExampleCodeGenerator(context);
        exampleCodeGenerator.generateCode({});
        const scenarioTestDataRender = new ScenarioTestDataRender(context);
        scenarioTestDataRender.renderData();
        const scenarioTestCodeGenerator = new ScenarioTestCodeGenerator(context);
        scenarioTestCodeGenerator.generateCode({});

        let exampleFiles = 0;
        for (const group of testCodeModel.codeModel.operationGroups) {
            for (const op of group.operations) {
                if (Object.keys(op.extensions?.[ExtensionName.xMsExamples]).length > 0) {
                    exampleFiles += 1;
                    break;
                }
            }
        }

        expect(spyCodeGenerate).toHaveBeenCalledTimes(1 /* mock test */ + exampleFiles + testCodeModel.codeModel.testModel.scenarioTests.length);
        expect(outputs).toMatchSnapshot();
    });
});

describe('GoTestGenerator from RP signalR', () => {
    let testCodeModel: TestCodeModeler;
    beforeAll(async () => {
        const codeModel = MockTool.loadCodeModel('signalR/test-modeler.yaml');
        const swaggerFolder = path.join(__dirname, '..', '..', '..', '..', '..', 'swagger/specification/signalr/resource-manager/');
        testCodeModel = TestCodeModeler.createInstance(
            codeModel as any,
            new TestConfig(
                {
                    __parents: {
                        'Microsoft.SignalRService/preview/2021-06-01-preview/signalr.json': process.platform.toLowerCase().startsWith('win')
                            ? `file:///${swaggerFolder}`
                            : `file://${swaggerFolder}`,
                    },
                    'input-file': ['Microsoft.SignalRService/preview/2021-06-01-preview/signalr.json'],
                    'test-resources': [
                        {
                            test: 'Microsoft.SignalRService/preview/2021-06-01-preview/scenarios/signalR.yaml',
                        },
                    ],
                    testmodeler: {
                        'split-parents-value': true,
                    },
                },
                configDefaults,
            ),
        );
        if (!process.platform.toLowerCase().startsWith('win')) {
            for (const scenarios of testCodeModel.codeModel.testModel.scenarioTests || []) {
                if (scenarios._filePath) {
                    scenarios._filePath = scenarios._filePath.split('\\').join('/');
                }
            }
        }
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('Generate scenario test', async () => {
        const outputs = {};
        const spyCodeGenerate = jest.spyOn(BaseCodeGenerator.prototype as any, 'writeToHost').mockImplementation((filename: string, output: string) => {
            outputs[filename] = output;
        });

        const context = new GenerateContext(undefined, testCodeModel.codeModel, new TestConfig({}, configDefaults));
        const mockTestDataRender = new MockTestDataRender(context);
        mockTestDataRender.renderData();
        const scenarioTestDataRender = new ScenarioTestDataRender(context);
        scenarioTestDataRender.renderData();
        const scenarioTestCodeGenerator = new ScenarioTestCodeGenerator(context);
        scenarioTestCodeGenerator.generateCode({});

        expect(spyCodeGenerate).toHaveBeenCalledTimes(testCodeModel.codeModel.testModel.scenarioTests.length);
        expect(outputs).toMatchSnapshot();
    });
});
