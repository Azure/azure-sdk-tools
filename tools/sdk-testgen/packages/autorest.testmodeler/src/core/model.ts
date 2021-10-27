import 'reflect-metadata';
import * as _ from 'lodash';
import * as fs from 'fs';
import * as path from 'path';
import {
    ArraySchema,
    CodeModel,
    ComplexSchema,
    DictionarySchema,
    ImplementationLocation,
    Languages,
    ObjectSchema,
    Operation,
    OperationGroup,
    Parameter,
    Schema,
    SchemaResponse,
    SchemaType,
    codeModelSchema,
} from '@autorest/codemodel';
import { Config, OavStepType, TestScenarioVariableNames, variableDefaults } from '../common/constant';
import { Helper } from '../util/helper';
import { Host, startSession } from '@autorest/extension-base';
import { TestConfig } from '../common/testConfig';
import { TestDefinitionFile, TestScenario, TestStep, TestStepArmTemplateDeployment, TestStepRestCall } from 'oav/dist/lib/testScenario/testResourceTypes';
import { TestResourceLoader } from 'oav/dist/lib/testScenario/testResourceLoader';

export enum ExtensionName {
    xMsExamples = 'x-ms-examples',
}
export interface ExampleExtensionResponse {
    body: any;
    headers: Record<string, any>;
}
export interface ExampleExtension {
    parameters?: Record<string, any>;
    responses?: Record<string, ExampleExtensionResponse>;
    'x-ms-original-file'?: string;
}

export type TestStepModel = {
    outputVariableNames: string[];
};

export type TestStepArmTemplateDeploymentModel = TestStepArmTemplateDeployment & TestStepModel;

export type TestStepRestCallModel = TestStepRestCall & TestStepModel & { exampleModel: ExampleModel };

/**
 * Generally a test group should be generated into one test source file.
 */
export class MockTestDefinitionModel {
    exampleGroups: ExampleGroup[] = [];
    public static groupByOperationGroup(exampleGroups: ExampleGroup[]): Record<string, ExampleGroup[]> {
        return exampleGroups.reduce((r, exampleGroup) => {
            const groupKey = exampleGroup.operationId.split('_')[0];
            r[groupKey] = r[groupKey] || [];
            r[groupKey].push(exampleGroup);
            return r;
        }, Object.create(null));
    }
}

export class ExampleGroup {
    operationId: string;
    examples: ExampleModel[] = [];
    public constructor(operationId: string) {
        this.operationId = operationId;
    }
}

export class TestModel {
    mockTest: MockTestDefinitionModel = new MockTestDefinitionModel();
    scenarioTests: TestDefinitionModel[] = [];
}

export interface TestCodeModel extends CodeModel {
    testModel?: TestModel;
}

export type TestScenarioModel = TestScenario & {
    requiredVariablesDefault?: { [variable: string]: string };
};

export type TestDefinitionModel = TestDefinitionFile & {
    useArmTemplate: boolean;
    requiredVariablesDefault: { [variable: string]: string };
    outputVariableNames: string[];
};
export class ExampleValue {
    language: Languages;
    schema: Schema;
    flattenedNames?: string[];

    /**Use elements if schema.type==Array, use properties if schema.type==Object/Dictionary, otherwise use rawValue */
    rawValue?: any;
    elements?: ExampleValue[];
    properties?: Record<string, ExampleValue>;

    parentsValue?: Record<string, ExampleValue>; // parent class Name--> value

    public constructor(schema: Schema = undefined, language: Languages = undefined) {
        this.language = language;
        this.schema = schema;
    }

    public get isNull(): boolean {
        return (
            (this.rawValue === null || this.rawValue === undefined) &&
            (this.elements === null || this.elements === undefined) &&
            (this.properties === null || this.properties === undefined)
        );
    }

    public static createInstance(rawValue: any, schema: Schema, language: Languages, searchDescents = true): ExampleValue {
        const instance = new ExampleValue(schema, language);
        if (!schema) {
            instance.rawValue = rawValue;
            return instance;
        }

        if (schema.type === SchemaType.Array && Array.isArray(rawValue)) {
            instance.elements = rawValue.map((x) => this.createInstance(x, (schema as ArraySchema).elementType, undefined));
        } else if (schema.type === SchemaType.Object && rawValue === Object(rawValue)) {
            const childSchema: ComplexSchema = searchDescents ? Helper.findInDescents(schema as ObjectSchema, rawValue) : schema;
            instance.schema = childSchema;

            instance.properties = {};
            const splitParentsValue = TestCodeModeler.instance.testConfig.getValue(Config.splitParentsValue, false);
            for (const property of Helper.getAllProperties(childSchema, !splitParentsValue)) {
                if (property.flattenedNames) {
                    const value = Helper.queryByPath(rawValue, property.flattenedNames);
                    if (value.length === 1) {
                        instance.properties[property.serializedName] = this.createInstance(value[0], property.schema, property.language);
                        instance.properties[property.serializedName].flattenedNames = property.flattenedNames;
                    }
                } else {
                    if (Object.prototype.hasOwnProperty.call(rawValue, property.serializedName)) {
                        instance.properties[property.serializedName] = this.createInstance(rawValue[property.serializedName], property.schema, property.language);
                    }
                }
            }

            instance.parentsValue = {};
            if (splitParentsValue && Object.prototype.hasOwnProperty.call(childSchema, 'parents') && (childSchema as ObjectSchema).parents) {
                for (const parent of (childSchema as ObjectSchema).parents.immediate) {
                    if (childSchema.type === SchemaType.Object) {
                        const parentValue = this.createInstance(rawValue, parent, parent.language, false);
                        if (Object.keys(parentValue.properties).length !== 0 || Object.keys(parentValue.parentsValue).length !== 0) {
                            instance.parentsValue[parent.language.default.name] = parentValue;
                        }
                    } else {
                        console.warn(`${parent.language.default.name} is NOT a object type of parent of ${childSchema.language.default.name}!`);
                    }
                }
            }
        } else if (schema.type === SchemaType.Dictionary && rawValue === Object(rawValue)) {
            instance.properties = {};
            for (const [key, value] of Object.entries(rawValue)) {
                instance.properties[key] = this.createInstance(value, (schema as DictionarySchema).elementType, undefined);
            }
        } else {
            instance.rawValue = rawValue;
        }
        return instance;
    }
}

export class ExampleParameter {
    /** Ref to the Parameter of operations in codeModel */
    parameter: Parameter;
    exampleValue: ExampleValue;

    public constructor(parameter: Parameter, rawValue: any) {
        this.parameter = parameter;
        this.exampleValue = ExampleValue.createInstance(rawValue, parameter?.schema, parameter.language);
        if (this.parameter.protocol?.http?.in === 'query' && typeof this.exampleValue.rawValue === 'string') {
            this.exampleValue.rawValue = decodeURIComponent(this.exampleValue.rawValue.replace(/\+/g, '%20'));
        }
    }
}

export class ExampleResponse {
    body?: ExampleValue;
    headers?: Record<string, any>;

    public static createInstance(rawResponse: ExampleExtensionResponse, schema: Schema, language: Languages): ExampleResponse {
        const instance = new ExampleResponse();
        if (rawResponse.body !== undefined) {
            instance.body = ExampleValue.createInstance(rawResponse.body, schema, language);
        }
        instance.headers = rawResponse.headers;
        return instance;
    }
}

export class ExampleModel {
    /** Key in x-ms-examples */
    name: string;
    operationGroup: OperationGroup;
    operation: Operation;
    clientParameters: ExampleParameter[] = [];
    methodParameters: ExampleParameter[] = [];
    responses: Record<string, ExampleResponse> = {}; // statusCode-->ExampleResponse
    originalFile: string;

    public constructor(name: string, operation: Operation, operationGroup: OperationGroup) {
        this.name = name;
        this.operation = operation;
        this.operationGroup = operationGroup;
    }
}

export class TestCodeModeler {
    public static instance: TestCodeModeler;
    public testConfig: TestConfig;
    private constructor(public codeModel: TestCodeModel, testConfig: TestConfig | Record<string, any>) {
        this.createTestConfig(testConfig);
    }

    private createTestConfig(testConfig: TestConfig | Record<string, any>) {
        if (!(testConfig instanceof TestConfig)) {
            this.testConfig = new TestConfig(testConfig);
        } else {
            this.testConfig = testConfig;
        }
    }

    public static createInstance(codeModel: TestCodeModel, testConfig: TestConfig | Record<string, any>): TestCodeModeler {
        if (TestCodeModeler.instance) {
            TestCodeModeler.instance.codeModel = codeModel;
            TestCodeModeler.instance.createTestConfig(testConfig);
        }
        TestCodeModeler.instance = new TestCodeModeler(codeModel, testConfig);
        return TestCodeModeler.instance;
    }

    private createExampleModel(exampleExtension: ExampleExtension, exampleName, operation: Operation, operationGroup: OperationGroup): ExampleModel {
        const parametersInExample = exampleExtension.parameters;
        const exampleModel = new ExampleModel(exampleName, operation, operationGroup);
        exampleModel.originalFile = Helper.getExampleRelativePath(exampleExtension['x-ms-original-file']);
        for (const parameter of Helper.allParameters(operation)) {
            if (parameter.flattened) {
                continue;
            }
            const paramRawData = Helper.queryByPath(parametersInExample, Helper.getFlattenedNames(parameter));
            if (paramRawData.length === 1) {
                const exampleParameter = new ExampleParameter(parameter, paramRawData[0]);
                if (parameter.implementation === ImplementationLocation.Method) {
                    exampleModel.methodParameters.push(exampleParameter);
                } else if (parameter.implementation === ImplementationLocation.Client) {
                    exampleModel.clientParameters.push(exampleParameter);
                } else {
                    // ignore
                }
            }
        }

        function findResponseSchema(statusCode: string): SchemaResponse {
            for (const response of operation.responses || []) {
                if ((response.protocol.http?.statusCodes || []).indexOf(statusCode) >= 0) {
                    return response as SchemaResponse;
                }
            }
            return undefined;
        }

        for (const [statusCode, response] of Object.entries(exampleExtension.responses)) {
            const exampleExtensionResponse = response;
            const schemaResponse = findResponseSchema(statusCode);
            if (schemaResponse) {
                exampleModel.responses[statusCode] = ExampleResponse.createInstance(exampleExtensionResponse, schemaResponse.schema, schemaResponse.language);
            }
        }
        return exampleModel;
    }

    public initiateTests() {
        if (!this.codeModel.testModel) {
            this.codeModel.testModel = new TestModel();
        }
    }

    public genMockTests() {
        this.initiateTests();
        this.codeModel.operationGroups.forEach((operationGroup) => {
            operationGroup.operations.forEach((operation) => {
                const exampleGroup = new ExampleGroup(operationGroup.language.default.name + '_' + operation.language.default.name);
                for (const [exampleName, rawValue] of Object.entries(operation.extensions?.[ExtensionName.xMsExamples] ?? {})) {
                    if (!this.testConfig.isDisabledExample(exampleName)) {
                        exampleGroup.examples.push(this.createExampleModel(rawValue as ExampleExtension, exampleName, operation, operationGroup));
                    }
                }
                this.codeModel.testModel.mockTest.exampleGroups.push(exampleGroup);
            });
        });
    }

    public static async getSessionFromHost(host: Host) {
        return await startSession<TestCodeModel>(host, {}, codeModelSchema);
    }

    public findOperationByOperationId(operationId: string): {
        operation: Operation;
        operationGroup: OperationGroup;
    } {
        let [groupKey, operationName] = operationId.split('_');
        if (!operationName) {
            operationName = groupKey;
            groupKey = '';
        }
        for (const group of this.codeModel.operationGroups) {
            if (group.$key !== groupKey) {
                continue;
            }
            for (const op of group.operations) {
                if (op.language.default.name === operationName) {
                    return {
                        operation: op,
                        operationGroup: group,
                    };
                }
            }
        }
        return undefined;
    }

    private initiateOavVariables(scope: Record<string, any>) {
        // set default values for requiredVariables
        if (scope.requiredVariables) {
            if (!scope.requiredVariablesDefault) {
                scope.requiredVariablesDefault = {};
            }
            const defaults = {
                ...variableDefaults,
                ...this.testConfig.getValue(Config.scenarioVariableDefaults),
            };
            for (const variable of scope.requiredVariables) {
                scope.requiredVariablesDefault[variable] = _.get(defaults, variable, '');
            }
            if (scope['scope'] && (scope['scope'] as string).toLocaleLowerCase() === 'resourcegroup') {
                scope.requiredVariablesDefault[TestScenarioVariableNames.resourceGroupName] = _.get(defaults, TestScenarioVariableNames.resourceGroupName, '');
            }
        }

        // format variable names
        if (scope.variables) {
            for (const [k, v] of Object.entries(scope.variables)) {
                if (!/^\w+$/.test(k)) {
                    const formatedName = Helper.toCamelCase(k);
                    delete Object.assign(scope.variables, { [formatedName]: v })[k];
                }
            }
        }
    }

    public initiateArmTemplate(testDef: TestDefinitionModel, stepModel: TestStepArmTemplateDeploymentModel) {
        if (!stepModel.armTemplateParametersPayload) {
            stepModel.armTemplateParametersPayload = {
                parameters: {},
            };
        }
        stepModel.outputVariableNames = [];
        for (const templateOutput of Object.keys(stepModel.armTemplatePayload.outputs || {})) {
            if (_.has(testDef.variables, templateOutput) || _.has(stepModel.variables, templateOutput)) {
                stepModel.outputVariableNames.push(templateOutput);
            }

            if (testDef.outputVariableNames.indexOf(templateOutput) < 0) {
                testDef.outputVariableNames.push(templateOutput);
            }
        }
    }

    public initiateRestCall(testDef: TestDefinitionModel, step: TestStepRestCall) {
        const operationInfo = this.findOperationByOperationId(step.operationId);
        if (!operationInfo) {
            throw new Error(`Can't find operationId ${step.operationId}[step ${step.exampleId}] in codeModel!`);
        }
        const { operation, operationGroup } = operationInfo;
        (step as TestStepRestCallModel).exampleModel = this.createExampleModel(
            {
                parameters: step.requestParameters,
                responses: {
                    [step.statusCode]: {
                        body: step.responseExpected,
                        headers: {},
                    },
                },
            },
            step.exampleId,
            operation,
            operationGroup,
        );

        for (const outputVariableName of Object.keys(step.outputVariables || {})) {
            if (testDef.outputVariableNames.indexOf(outputVariableName) < 0) {
                testDef.outputVariableNames.push(outputVariableName);
            }
        }

        // Remove oav operation to save size of codeModel.
        // Don't do this if oav operation is used in future.
        if (Object.prototype.hasOwnProperty.call(step, 'operation')) {
            (step as any).operation = undefined;
        }
    }

    public initiateTestDefinition(testDef: TestDefinitionModel) {
        this.initiateOavVariables(testDef);
        testDef.useArmTemplate = false;
        testDef.outputVariableNames = [];

        const allSteps: TestStep[] = [...testDef.prepareSteps];
        for (const scenario of testDef.testScenarios as TestScenarioModel[]) {
            allSteps.push(...scenario.steps);
            this.initiateOavVariables(scenario);
        }

        for (const step of allSteps) {
            this.initiateOavVariables(step);
            if (step.type === OavStepType.restCall) {
                this.initiateRestCall(testDef, step);
            } else if (step.type === OavStepType.armTemplate) {
                testDef.useArmTemplate = true;
                this.initiateArmTemplate(testDef, step as TestStepArmTemplateDeploymentModel);
            }
        }
    }

    public async loadTestResources() {
        try {
            const fileRoot = this.testConfig.getSwaggerFolder();
            const loader = TestResourceLoader.create({
                useJsonParser: false,
                checkUnderFileRoot: false,
                fileRoot: fileRoot,
                swaggerFilePaths: this.testConfig.getValue(Config.inputFile),
            });

            for (const testResource of this.testConfig.getValue(Config.testResources) || []) {
                if (fs.existsSync(path.join(fileRoot, testResource[Config.test]))) {
                    try {
                        const testDef = (await loader.load(testResource[Config.test])) as TestDefinitionModel;
                        this.initiateTestDefinition(testDef);
                        this.codeModel.testModel.scenarioTests.push(testDef);
                    } catch (error) {
                        console.warn(`Exception occured when load testdef ${testResource[Config.test]}: ${error}`);
                    }
                } else {
                    console.warn(`Unexisted test resource scenario file: ${testResource[Config.test]}`);
                }
            }
        } catch (error) {
            console.warn('Exception occured when load test resource scenario!');
            console.warn(`${__filename} - FAILURE  ${JSON.stringify(error)} ${error.stack}`);
        }
    }
}
