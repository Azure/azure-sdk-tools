import 'reflect-metadata';
import * as _ from 'lodash';
import * as fs from 'fs';
import * as path from 'path';
import { ApiScenarioLoader } from 'oav/dist/lib/apiScenario/apiScenarioLoader';
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
import { AutorestExtensionHost, startSession } from '@autorest/extension-base';
import { Config, OavStepType, testScenarioVariableDefault } from '../common/constant';
import { Helper } from '../util/helper';
import { Scenario, ScenarioDefinition, Step, StepArmTemplate, StepRestCall } from 'oav/dist/lib/apiScenario/apiScenarioTypes';
import { TestConfig } from '../common/testConfig';

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

export type StepArmTemplateModel = StepArmTemplate & TestStepModel;

export type StepRestCallModel = StepRestCall & TestStepModel & { exampleModel: ExampleModel };

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
    operationGroup: OperationGroup;
    operation: Operation;
    examples: ExampleModel[] = [];
    public constructor(operationGroup: OperationGroup, operation: Operation, operationId: string) {
        this.operationGroup = operationGroup;
        this.operation = operation;
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

export type TestScenarioModel = Scenario & {
    requiredVariablesDefault?: { [variable: string]: string };
};

export type TestDefinitionModel = ScenarioDefinition & {
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

    public static createInstance(rawValue: any, usedProperties: Set<string[]>, schema: Schema, language: Languages, searchDescents = true): ExampleValue {
        const instance = new ExampleValue(schema, language);
        if (!schema) {
            instance.rawValue = rawValue;
            return instance;
        }

        function addParentValue(parent: ComplexSchema) {
            const parentValue = ExampleValue.createInstance(rawValue, usedProperties, parent, parent.language, false);
            if ((parentValue.properties && Object.keys(parentValue.properties).length > 0) || (parentValue.parentsValue && Object.keys(parentValue.parentsValue).length > 0)) {
                instance.parentsValue[parent.language.default.name] = parentValue;
            }
        }

        if (schema.type === SchemaType.Array && Array.isArray(rawValue)) {
            instance.elements = rawValue.map((x) => this.createInstance(x, new Set(), (schema as ArraySchema).elementType, undefined));
        } else if (schema.type === SchemaType.Object && rawValue === Object(rawValue)) {
            const childSchema: ComplexSchema = searchDescents ? Helper.findInDescents(schema as ObjectSchema, rawValue) : schema;
            instance.schema = childSchema;

            instance.properties = {};
            const splitParentsValue = TestCodeModeler.instance.testConfig.getValue(Config.splitParentsValue);
            for (const property of Helper.getAllProperties(childSchema, !splitParentsValue)) {
                if (property.flattenedNames) {
                    if (!Helper.pathIsIncluded(usedProperties, property.flattenedNames)) {
                        const value = Helper.queryByPath(rawValue, property.flattenedNames);
                        if (value.length === 1) {
                            instance.properties[property.serializedName] = this.createInstance(
                                value[0],
                                Helper.filterPathsByPrefix(usedProperties, property.flattenedNames),
                                property.schema,
                                property.language,
                            );
                            instance.properties[property.serializedName].flattenedNames = property.flattenedNames;
                            usedProperties.add(property.flattenedNames);
                        }
                    }
                } else {
                    if (Object.prototype.hasOwnProperty.call(rawValue, property.serializedName) && !Helper.pathIsIncluded(usedProperties, [property.serializedName])) {
                        instance.properties[property.serializedName] = this.createInstance(
                            rawValue[property.serializedName],
                            Helper.filterPathsByPrefix(usedProperties, [property.serializedName]),
                            property.schema,
                            property.language,
                        );
                        usedProperties.add([property.serializedName]);
                    }
                }
            }

            instance.parentsValue = {};
            // Add normal parentValues
            if (splitParentsValue && Object.prototype.hasOwnProperty.call(childSchema, 'parents') && (childSchema as ObjectSchema).parents) {
                for (const parent of (childSchema as ObjectSchema).parents.immediate) {
                    if (childSchema.type === SchemaType.Object) {
                        addParentValue(parent);
                    } else {
                        console.warn(`${parent.language.default.name} is NOT a object type of parent of ${childSchema.language.default.name}!`);
                    }
                }
            }

            // Add AdditionalProperties as ParentValue
            if (Object.prototype.hasOwnProperty.call(childSchema, 'parents') && (childSchema as ObjectSchema).parents) {
                for (const parent of (childSchema as ObjectSchema).parents.immediate) {
                    if (parent.type === SchemaType.Dictionary) {
                        addParentValue(parent);
                    }
                }
            }
        } else if (schema.type === SchemaType.Dictionary && rawValue === Object(rawValue)) {
            instance.properties = {};
            for (const [key, value] of Object.entries(rawValue)) {
                if (!Helper.pathIsIncluded(usedProperties, [key])) {
                    instance.properties[key] = this.createInstance(value, new Set([...usedProperties]), (schema as DictionarySchema).elementType, undefined);
                    usedProperties.add([key]);
                }
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
        this.exampleValue = ExampleValue.createInstance(rawValue, new Set(), parameter?.schema, parameter.language);
    }
}

export class ExampleResponse {
    body?: ExampleValue;
    headers?: Record<string, any>;

    public static createInstance(rawResponse: ExampleExtensionResponse, schema: Schema, language: Languages): ExampleResponse {
        const instance = new ExampleResponse();
        if (rawResponse.body !== undefined) {
            instance.body = ExampleValue.createInstance(rawResponse.body, new Set(), schema, language);
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
    private constructor(public codeModel: TestCodeModel, testConfig: TestConfig) {
        this.testConfig = testConfig;
    }

    public static createInstance(codeModel: TestCodeModel, testConfig: TestConfig): TestCodeModeler {
        if (TestCodeModeler.instance) {
            TestCodeModeler.instance.codeModel = codeModel;
            TestCodeModeler.instance.testConfig = testConfig;
        }
        TestCodeModeler.instance = new TestCodeModeler(codeModel, testConfig);
        return TestCodeModeler.instance;
    }

    private createExampleModel(exampleExtension: ExampleExtension, exampleName, operation: Operation, operationGroup: OperationGroup): ExampleModel {
        const allParameters = Helper.allParameters(operation);
        const parametersInExample = exampleExtension.parameters;
        const exampleModel = new ExampleModel(exampleName, operation, operationGroup);
        exampleModel.originalFile = Helper.getExampleRelativePath(exampleExtension['x-ms-original-file']);
        for (const parameter of allParameters) {
            if (parameter.flattened) {
                continue;
            }
            const t = Helper.getFlattenedNames(parameter);
            const paramRawData = parameter.protocol?.http?.in === 'body' ? Helper.queryBodyParameter(parametersInExample, t) : Helper.queryByPath(parametersInExample, t);
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
        if (!this.testConfig.getValue(Config.useExampleModel)) {
            return;
        }
        this.codeModel.operationGroups.forEach((operationGroup) => {
            operationGroup.operations.forEach((operation) => {
                const operationId = operationGroup.language.default.name + '_' + operation.language.default.name;
                // TODO: skip non-json http bodys for now. Need to validate example type with body schema to support it.
                const mediaTypes = operation.requests[0]?.protocol?.http?.mediaTypes;
                if (mediaTypes && mediaTypes.indexOf('application/json') < 0) {
                    console.warn(`genMockTests: MediaTypes ${operation.requests[0]?.protocol?.http?.mediaTypes} in operation ${operationId} is not supported!`);
                    return;
                }
                const exampleGroup = new ExampleGroup(operationGroup, operation, operationId);
                for (const [exampleName, rawValue] of Object.entries(operation.extensions?.[ExtensionName.xMsExamples] ?? {})) {
                    if (!this.testConfig.isDisabledExample(exampleName)) {
                        exampleGroup.examples.push(this.createExampleModel(rawValue as ExampleExtension, exampleName, operation, operationGroup));
                    }
                }
                this.codeModel.testModel.mockTest.exampleGroups.push(exampleGroup);
            });
        });
    }

    public static async getSessionFromHost(host: AutorestExtensionHost) {
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
                ...testScenarioVariableDefault,
                ...this.testConfig.getValue(Config.scenarioVariableDefaults),
            };
            for (const variable of scope.requiredVariables) {
                scope.requiredVariablesDefault[variable] = _.get(defaults, variable, '');
            }
            if (scope['scope'] && (scope['scope'] as string).toLocaleLowerCase() === 'resourcegroup') {
                scope.requiredVariablesDefault['resourceGroupName'] = _.get(defaults, 'resourceGroupName', '');
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

    public initiateArmTemplate(testDef: TestDefinitionModel, stepModel: StepArmTemplateModel) {
        stepModel.outputVariableNames = [];
        for (const templateOutput of Object.keys(stepModel.armTemplatePayload.outputs || {})) {
            if (_.has(testDef.variables, templateOutput) || _.has(stepModel.variables, templateOutput)) {
                stepModel.outputVariableNames.push(templateOutput);
            }

            if (testDef.outputVariableNames.indexOf(templateOutput) < 0) {
                testDef.outputVariableNames.push(templateOutput);
            }
        }

        const scriptContentKey = 'scriptContent';
        for (const resource of stepModel.armTemplatePayload?.resources || []) {
            const scriptContentValue = resource.properties?.[scriptContentKey];
            if (scriptContentValue && typeof scriptContentValue === 'string') {
                if (process.platform.toLowerCase().startsWith('win')) {
                    // align new line character for scriptContent across win/os/linux
                    resource.properties[scriptContentKey] = scriptContentValue.split('\r\n').join('\n');
                }
            }
        }
    }

    public initiateRestCall(testDef: TestDefinitionModel, step: StepRestCallModel) {
        const operationInfo = this.findOperationByOperationId(step.operationId);
        if (operationInfo) {
            const { operation, operationGroup } = operationInfo;
            if (this.testConfig.getValue(Config.useExampleModel)) {
                step.exampleModel = this.createExampleModel(
                    {
                        parameters: step.requestParameters,
                        responses: {
                            [step.statusCode]: {
                                body: step.expectedResponse,
                                headers: {},
                            },
                        },
                    },
                    step.exampleName,
                    operation,
                    operationGroup,
                );
            }

            for (const outputVariableName of Object.keys(step.outputVariables || {})) {
                if (testDef.outputVariableNames.indexOf(outputVariableName) < 0) {
                    testDef.outputVariableNames.push(outputVariableName);
                }
            }

            // Remove oav operation to save size of codeModel.
            // Don't do this if oav operation is used in future.
            if (Object.prototype.hasOwnProperty.call(step, 'operation')) {
                step.operation = undefined;
            }
        }
    }

    public initiateTestDefinition(testDef: TestDefinitionModel, codeModelRestcallOnly = false) {
        this.initiateOavVariables(testDef);
        testDef.useArmTemplate = false;
        testDef.outputVariableNames = [];

        const allSteps: Step[] = [...testDef.prepareSteps];
        for (const scenario of testDef.scenarios as TestScenarioModel[]) {
            allSteps.push(...scenario.steps);
            this.initiateOavVariables(scenario);
        }
        allSteps.push(...testDef.cleanUpSteps);

        for (const step of allSteps) {
            this.initiateOavVariables(step);
            if (step.type === OavStepType.restCall) {
                const stepModel = step as StepRestCallModel;
                this.initiateRestCall(testDef, stepModel);
                if (codeModelRestcallOnly && !stepModel.exampleModel) {
                    throw new Error(`Can't find operationId ${step.operationId}[step ${step.exampleName}] in codeModel!`);
                }
            } else if (step.type === OavStepType.armTemplate) {
                testDef.useArmTemplate = true;
                this.initiateArmTemplate(testDef, step as StepArmTemplateModel);
            }
        }
    }

    public async loadTestResources() {
        try {
            const fileRoot = this.testConfig.getSwaggerFolder();
            const loader = ApiScenarioLoader.create({
                useJsonParser: false,
                checkUnderFileRoot: false,
                fileRoot: fileRoot,
                swaggerFilePaths: this.testConfig.getValue(Config.inputFile),
            });

            if (Array.isArray(this.testConfig.config[Config.testResources])) {
                await this.loadTestResourcesFromConfig(fileRoot, loader);
            } else {
                await this.loadAvailableTestResources(fileRoot, loader);
            }
        } catch (error) {
            console.warn('Exception occured when load test resource scenario!');
            console.warn(`${__filename} - FAILURE  ${JSON.stringify(error)} ${error.stack}`);
        }
    }

    public async loadTestResourcesFromConfig(fileRoot: string, loader: ApiScenarioLoader) {
        for (const testResource of this.testConfig.getValue(Config.testResources)) {
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
    }

    public async loadAvailableTestResources(fileRoot: string, loader: ApiScenarioLoader) {
        const scenariosFolder = 'scenarios';
        const codemodelRestCallOnly = this.testConfig.getValue(Config.scenarioCodeModelRestCallOnly);
        for (const apiFolder of this.testConfig.getInputFileFolders()) {
            const scenarioPath = path.join(fileRoot, apiFolder, scenariosFolder);
            // currently loadAvailableTestResources only support scenario scanning from local file system
            if (fs.existsSync(scenarioPath) && fs.lstatSync(scenarioPath).isDirectory()) {
                for (const scenarioFile of fs.readdirSync(scenarioPath)) {
                    if (!scenarioFile.endsWith('.yaml') && !scenarioFile.endsWith('.yml')) {
                        continue;
                    }
                    const scenarioPathName = path.join(apiFolder, scenariosFolder, scenarioFile);
                    try {
                        const testDef = (await loader.load(scenarioPathName)) as TestDefinitionModel;

                        this.initiateTestDefinition(testDef, codemodelRestCallOnly);
                        this.codeModel.testModel.scenarioTests.push(testDef);
                    } catch {
                        console.warn(`${scenarioPathName} is not a valid api scenario`);
                    }
                }
            }
        }
    }
}
