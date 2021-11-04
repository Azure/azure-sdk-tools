/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as _ from 'lodash';
import * as path from 'path';
import { ArraySchema, ChoiceSchema, DateTimeSchema, DictionarySchema, GroupProperty, Metadata, ObjectSchema, Parameter, Schema, SchemaType } from '@autorest/codemodel';
import { Config, TargetMode, variableDefaults } from '../common/constant';
import {
    ExampleModel,
    ExampleParameter,
    ExampleValue,
    MockTestDefinitionModel,
    TestCodeModel,
    TestCodeModeler,
    TestDefinitionModel,
    TestScenarioModel,
    TestStepArmTemplateDeploymentModel,
    TestStepRestCallModel,
} from '@autorest/testmodeler/dist/src/core/model';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { Host } from '@autorest/extension-base';
import { ImportManager } from '@autorest/go/dist/generator/imports';
import { OavStepType } from '@autorest/testmodeler/dist/src/common/constant';
import { TestConfig } from '@autorest/testmodeler/dist/src/common/testConfig';
import { TestGenerator } from './testGenerator';
import { TestStep } from 'oav/dist/lib/testScenario/testResourceTypes';
import { generateReturnsInfo, getAPIParametersSig, getClientParametersSig, getSchemaResponse } from '../util/codegenBridge';
import { isLROOperation, isPageableOperation } from '@autorest/go/dist/common/helpers';

export async function processRequest(host: Host): Promise<void> {
    const session = await TestCodeModeler.getSessionFromHost(host);
    const config = await session.getValue('');
    if (_.get(config, Config.exportCodemodel, false)) {
        Helper.addCodeModelDump(session, 'go-tester-pre.yaml');
    }
    const generator = await new GoTestGenerator(host, session.model, new TestConfig(config));
    generator.genRenderData();
    const extraParam = { copyright: await Helper.getCopyright(session) };
    if (_.get(config, Config.generateMockTest, true)) {
        await generator.generateMockTest('mockTest.go.njk', extraParam);
    }
    if (_.get(config, Config.generateSdkExample, false)) {
        generator.generateExample('exampleTest.go.njk', extraParam);
    }
    if (_.get(config, Config.generateScenarioTest, false)) {
        await generator.generateScenarioTest('scenarioTest.go.njk', extraParam);
    }
    await Helper.outputToModelerfour(host, session);
    if (_.get(config, Config.exportCodemodel, false)) {
        Helper.addCodeModelDump(session, 'go-tester.yaml');
    }
    Helper.dump(host);
}

interface GoFileData {
    packageName: string;
    packagePath?: string;
    imports: string;
}
class GoMockTestDefinitionModel extends MockTestDefinitionModel implements GoFileData {
    packageName: string;
    imports: string;
}

export type GoTestDefinition = TestDefinitionModel & GoFileData;

class GoExampleModel extends ExampleModel {
    opName: string;
    isLRO: boolean;
    isPageable: boolean;
    methodParametersOutput: string;
    clientParametersOutput: string;
    methodParametersOutputForExample: string;
    clientParametersOutputForExample: string;
    returnInfo: string[];
    nonNilReturns: string[];
    checkResponse: boolean;
    pollerType: string;
    pageableType: string;
}

export class GoTestGenerator extends TestGenerator {
    importManager: ImportManager;
    definedVariables: Record<string, string> = {};

    public constructor(public host: Host, public codeModel: TestCodeModel, public testConfig: TestConfig) {
        super(host, codeModel, testConfig);
    }

    private getFilePrefix(configName: string) {
        let filePrefix = this.testConfig.getValue(configName, variableDefaults[configName]);
        if (filePrefix.length > 0 && filePrefix[filePrefix.length - 1] !== '_') {
            filePrefix += '_';
        }
        return filePrefix;
    }

    public getMockTestFilename() {
        return `${this.getFilePrefix(Config.testFilePrefix)}mock_test.go`;
    }

    public getScenarioTestFilename(testDef: TestDefinitionModel): string {
        const file = path.basename(testDef._filePath);
        const filename = file.split('.').slice(0, -1).join('.');
        return `scenario/${this.getFilePrefix(Config.testFilePrefix)}${filename}_test.go`;
    }

    public getLanguageName(meta: any): string {
        return (meta as Metadata).language.go.name;
    }

    private genParameterOutput(
        paramName: string,
        paramType: string,
        parameter: Parameter | GroupProperty,
        exampleParameters: ExampleParameter[],
        targetMode: TargetMode,
    ): string | undefined {
        const findExampleParameter = (name: string): string | undefined => {
            for (const methodParameter of exampleParameters) {
                if (this.getLanguageName(methodParameter.parameter) === name) {
                    return this.exampleValueToString(methodParameter.exampleValue, !methodParameter.parameter.required, targetMode);
                }
            }
            return undefined;
        };

        if ((parameter as GroupProperty).originalParameter) {
            const group = parameter as GroupProperty;
            const ptr = paramType.startsWith('*') ? '&' : '';
            const packageName: string = targetMode === TargetMode.sample || targetMode === TargetMode.scenarioTest ? this.codeModel.language.go.packageName + '.' : '';
            let ret = `${ptr}${packageName}${this.getLanguageName(parameter.schema)}{`;
            let hasContent = false;
            for (const insideParameter of group.originalParameter) {
                const insideOutput = findExampleParameter(this.getLanguageName(insideParameter));
                if (insideOutput) {
                    ret += `${this.getLanguageName(insideParameter)}: ${insideOutput},\n`;
                    hasContent = true;
                }
            }
            ret += '}';
            if ([TargetMode.sample, TargetMode.scenarioTest].indexOf(targetMode) >= 0 && ptr.length > 0 && !hasContent) {
                ret = 'nil';
            }
            return ret;
        }
        return findExampleParameter(paramName);
    }

    private toParameterOutput(paramsSig: Array<[string, string, Parameter | GroupProperty]>, exampleParameters: ExampleParameter[], targetMode: TargetMode): string {
        return paramsSig
            .map(([paramName, typeName, parameter]) => {
                if (parameter === undefined || parameter === null) {
                    return paramName;
                }
                return this.genParameterOutput(paramName, typeName, parameter, exampleParameters, targetMode) || 'nil';
            })
            .join(',\n');
    }

    private fillExampleOutput(example: GoExampleModel, targetMode: TargetMode) {
        const op = example.operation;
        example.opName = op.language.go.name;
        if (isLROOperation(op as any)) {
            // example.opName = op.language.go.protocolNaming.internalMethod
            example.opName = 'Begin' + example.opName;
            example.isLRO = true;
            this.importManager.add('time');
            example.pollerType = example.operation.language.go.responseEnv.language.go.name;
        } else {
            example.isLRO = false;
        }
        example.isPageable = isPageableOperation(op as any);
        example.methodParametersOutput = this.toParameterOutput(getAPIParametersSig(op), example.methodParameters, targetMode);
        example.clientParametersOutput = this.toParameterOutput(getClientParametersSig(example.operationGroup), example.clientParameters, targetMode);
        example.returnInfo = generateReturnsInfo(op, 'op');
        let responseSchema = getSchemaResponse(op as any)?.schema;
        if (!example.isLRO && example.isPageable) {
            const valueName = op.extensions['x-ms-pageable'].itemName === undefined ? 'value' : op.extensions['x-ms-pageable'].itemName;
            for (const property of responseSchema['properties']) {
                if (property.serializedName === valueName) {
                    responseSchema = property.schema.elementType;
                    break;
                }
            }
            example.pageableType = example.operation.language.go.pageableType.name;
        }
        const allReturnProperties = Helper.getAllProperties(responseSchema as any, true);
        example.nonNilReturns = [];
        for (const variable of ['ID']) {
            for (const p of allReturnProperties) {
                if (this.getLanguageName(p) === variable) {
                    example.nonNilReturns.push(`${this.getLanguageName(responseSchema)}.${variable}`);
                }
            }
        }
        example.checkResponse = example.nonNilReturns.length > 0;
    }

    public genRenderDataForStep(testDef: GoTestDefinition, step: TestStep) {
        switch (step.type) {
            case OavStepType.restCall: {
                const example = (step as TestStepRestCallModel).exampleModel as GoExampleModel;
                this.fillExampleOutput(example, TargetMode.scenarioTest);
                if (step.outputVariables && Object.keys(step.outputVariables).length > 0) {
                    this.importManager.add('github.com/go-openapi/jsonpointer');
                    example.checkResponse = true;
                }
                break;
            }
            case OavStepType.armTemplate: {
                testDef.useArmTemplate = true;
                this.importManager.add('encoding/json');
                (step as TestStepArmTemplateDeploymentModel).outputVariableNames = [];
                for (const templateOutput of Object.keys(step.armTemplatePayload.outputs || {})) {
                    if (_.has(testDef.variables, templateOutput) || _.has(step.variables, templateOutput)) {
                        (step as TestStepArmTemplateDeploymentModel).outputVariableNames.push(templateOutput);
                    }
                }
                break;
            }
            default:
        }
    }

    public genRenderDataForDefinition(testDef: GoTestDefinition) {
        this.importManager = new ImportManager();
        this.importManager.add('context');
        this.importManager.add('log');
        this.importManager.add('os');
        this.importManager.add('runtime/debug');
        this.importManager.add('testing');
        this.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/azcore');
        this.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/azcore/arm');
        this.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/azidentity');

        if (testDef.scope.toLowerCase() === 'resourcegroup') {
            this.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/resources/armresources');
        }
        testDef.packageName = this.codeModel.language.go.packageName;
        testDef.packagePath = this.testConfig.getValue(Config.module, `github.com/Azure/azure-sdk-for-go/sdk/${testDef.packageName.substr(3)}/${testDef.packageName}`);
        this.importManager.add(testDef.packagePath);

        for (const step of testDef.prepareSteps) {
            this.definedVariables = {
                ...testDef.requiredVariablesDefault,
                ...testDef.variables,
            };
            this.genRenderDataForStep(testDef, step);
        }
        for (const scenario of testDef.testScenarios as TestScenarioModel[]) {
            for (const step of scenario.steps) {
                this.definedVariables = {
                    ...testDef.requiredVariablesDefault,
                    ...scenario.requiredVariablesDefault,
                    ...testDef.variables,
                    ...scenario.variables,
                    ...step.variables,
                };
                this.genRenderDataForStep(testDef, step);
            }
        }

        testDef.imports = this.importManager.text();
    }

    public genRenderDataForMock() {
        this.importManager = new ImportManager();
        const mockTest = this.codeModel.testModel.mockTest as GoMockTestDefinitionModel;
        mockTest.packageName = this.codeModel.language.go.packageName;
        for (const exampleGroup of mockTest.exampleGroups) {
            for (const example of exampleGroup.examples as GoExampleModel[]) {
                this.fillExampleOutput(example, TargetMode.mockTest);
            }
        }
        mockTest.imports = this.importManager.text();
    }

    private isPrimitiveType(type: string) {
        const firstChar = type[0];
        return firstChar === firstChar.toLowerCase();
    }

    public exampleValueToString(exampleValue: ExampleValue, isPtr: boolean | undefined, targetMode: TargetMode, inArray = false): string {
        const packageName: string = targetMode === TargetMode.sample || targetMode === TargetMode.scenarioTest ? this.codeModel.language.go.packageName + '.' : '';
        if (exampleValue === null || exampleValue === undefined || exampleValue.isNull) {
            return 'nil';
        }
        const isPolymophismValue = exampleValue?.schema?.type === SchemaType.Object && (exampleValue.schema as ObjectSchema).discriminatorValue;
        const ptr = (exampleValue.language?.go?.byValue && !isPolymophismValue) || isPtr === false ? '' : '&';
        if (exampleValue.schema?.type === SchemaType.Array) {
            const elementPtr = exampleValue.schema.language.go.elementIsPtr ? '*' : '';
            const elementTypeName = this.getLanguageName((exampleValue.schema as ArraySchema).elementType);
            if (exampleValue.elements === undefined) {
                return `${ptr}[]${elementPtr}${this.isPrimitiveType(elementTypeName) ? '' : packageName}${elementTypeName}{}`;
            } else {
                return (
                    `${ptr}[]${elementPtr}${this.isPrimitiveType(elementTypeName) ? '' : packageName}${elementTypeName}{\n` +
                    exampleValue.elements.map((x) => this.exampleValueToString(x, exampleValue.schema.language.go.elementIsPtr, targetMode, true)).join(',\n') +
                    '}'
                );
            }
        } else if (exampleValue.schema?.type === SchemaType.Object) {
            let output = '';
            if (inArray) {
                output += `{\n`;
            } else {
                output += `${ptr}${packageName}${this.getLanguageName(exampleValue.schema)}{\n`;
            }
            for (const [_, parentValue] of Object.entries(exampleValue.parentsValue || {})) {
                output += `${this.getLanguageName(parentValue)}: ${this.exampleValueToString(parentValue, false, targetMode)},\n`;
            }
            for (const [_, value] of Object.entries(exampleValue.properties || {})) {
                output += `${this.getLanguageName(value)}: ${this.exampleValueToString(value, undefined, targetMode)},\n`;
            }
            output += '}';
            return output;
        } else if (exampleValue.schema?.type === SchemaType.Dictionary) {
            let output = `${ptr}map[string]${exampleValue.schema.language.go.elementIsPtr ? '*' : ''}${(exampleValue.schema as DictionarySchema).elementType.language.go.name}{\n`;
            for (const [key, value] of Object.entries(exampleValue.properties || {})) {
                output += `"${key}": ${this.exampleValueToString(value, exampleValue.schema.language.go.elementIsPtr, targetMode)},\n`;
            }
            output += '}';
            return output;
        }

        let rawValue = exampleValue.rawValue;
        if (targetMode === TargetMode.sample && this.getLanguageName(exampleValue.schema) === 'string' && exampleValue.language) {
            rawValue = '<' + Helper.toKebabCase(this.getLanguageName(exampleValue)) + '>';
        }
        return this.rawValueToString(rawValue, exampleValue.schema, isPtr === undefined ? !exampleValue.language.go.byValue : isPtr, targetMode);
    }

    public generateExample(templateFile: string, extraParam: any = {}) {
        for (const [groupKey, exampleGroups] of Object.entries(MockTestDefinitionModel.groupByOperationGroup(this.codeModel.testModel.mockTest.exampleGroups))) {
            this.importManager = new ImportManager();
            let hasExample = false;
            for (const exampleGroup of exampleGroups) {
                for (const example of exampleGroup.examples as GoExampleModel[]) {
                    example.methodParametersOutputForExample = this.toParameterOutput(getAPIParametersSig(example.operation), example.methodParameters, TargetMode.sample);
                    example.clientParametersOutputForExample = this.toParameterOutput(getClientParametersSig(example.operationGroup), example.clientParameters, TargetMode.sample);
                    if (example.isLRO) {
                        this.importManager.add('time');
                    }
                    hasExample = true;
                }
            }
            if (!hasExample) {
                continue;
            }

            // Render to template
            const tmplPath = path.relative(process.cwd(), path.join(`${__dirname}`, `../../src/template/${templateFile}`));
            const packageName: string = this.codeModel.language.go.packageName;
            this.writeToHost(
                `${this.getFilePrefix(Config.exampleFilePrefix)}example_${groupKey}_test.go`.toLowerCase(),
                this.render(tmplPath, {
                    exampleGroups: exampleGroups,
                    packageName: packageName,
                    packagePath: this.testConfig.getValue(Config.module, `github.com/Azure/azure-sdk-for-go/sdk/${packageName.substr(3)}/${packageName}`),
                    imports: this.importManager.text(),
                    ...extraParam,
                }),
            );
        }
    }

    public rawValueToString(rawValue: any, schema: Schema, isPtr: boolean, targetMode: TargetMode): string {
        let ret = JSON.stringify(rawValue);
        if (rawValue !== null && rawValue !== undefined && Object.getPrototypeOf(rawValue) === Object.prototype) {
            ret = '`' + ret + '`';
        }
        const goType = this.getLanguageName(schema);
        if ([SchemaType.Choice, SchemaType.SealedChoice].indexOf(schema.type) >= 0) {
            const choiceValue = Helper.findChoiceValue(schema as ChoiceSchema, rawValue);
            ret = this.getLanguageName(choiceValue);
            if (targetMode === TargetMode.sample || targetMode === TargetMode.scenarioTest) {
                ret = this.codeModel.language.go.packageName + '.' + ret;
            }
        }
        if (schema.type === SchemaType.Constant || goType === 'string') {
            if (targetMode === TargetMode.scenarioTest && typeof rawValue === 'string') {
                ret = this.parseOavVariable(rawValue, this.definedVariables).join(' + ');
            } else {
                ret = Helper.quotedEscapeString(rawValue);
            }
        } else if (goType === 'time.Time') {
            this.importManager.add('time');
            const timeFormat = (schema as DateTimeSchema).format === 'date-time-rfc1123' ? 'time.RFC1123' : 'time.RFC3339Nano';
            ret = `func() time.Time { t, _ := time.Parse(${timeFormat}, "${rawValue}"); return t}()`;
        } else if (goType === 'map[string]interface{}') {
            ret = this.obejctToString(rawValue);
        } else if (goType === 'bool') {
            ret = rawValue.toString();
        }

        if (isPtr) {
            const ptrConverts = {
                string: 'StringPtr',
                bool: 'BoolPtr',
                'time.Time': 'TimePtr',
                int32: 'Int32Ptr',
                int64: 'Int64Ptr',
                float32: 'Float32Ptr',
                float64: 'Float64Ptr',
            };

            if (schema.type === SchemaType.Constant) {
                ret = `to.StringPtr(${ret})`;
                this.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/azcore/to');
            } else if ([SchemaType.Choice, SchemaType.SealedChoice].indexOf(schema.type) >= 0) {
                ret += '.ToPtr()';
            } else if (Object.prototype.hasOwnProperty.call(ptrConverts, goType)) {
                ret = `to.${ptrConverts[goType]}(${ret})`;
                this.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/azcore/to');
            } else {
                ret = '&' + ret;
            }
        }

        return ret;
    }

    public obejctToString(rawValue: any) {
        let ret = `map[string]interface{}{\n`;
        for (const [key, value] of Object.entries(rawValue)) {
            if (_.isArray(value)) {
                ret += `"${key}":`;
                ret += this.arrayToString(value);
                ret += `,\n`;
            } else if (_.isObject(value)) {
                ret += `"${key}":`;
                ret += this.obejctToString(value);
                ret += `,\n`;
            } else if (_.isString(value)) {
                ret += `"${key}": ${Helper.quotedEscapeString(value)},\n`;
            } else {
                ret += `"${key}": ${value},\n`;
            }
        }
        ret += '}';
        return ret;
    }

    public arrayToString(rawValue: any) {
        let ret = `[]interface{}{\n`;
        for (const item of rawValue) {
            if (_.isArray(item)) {
                ret += this.arrayToString(item);
                `,\n`;
            } else if (_.isObject(item)) {
                ret += this.obejctToString(item);
                ret += `,\n`;
            } else if (_.isString(item)) {
                ret += `${Helper.quotedEscapeString(item)},\n`;
            } else {
                ret += `${item},\n`;
            }
        }
        ret += '}';
        return ret;
    }
}
