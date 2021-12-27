/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import {
    ArraySchema,
    ChoiceSchema,
    DateTimeSchema,
    DictionarySchema,
    GroupProperty,
    ImplementationLocation,
    Metadata,
    ObjectSchema,
    Parameter,
    Schema,
    SchemaType,
} from '@autorest/codemodel';
import { BaseCodeGenerator, BaseDataRender } from './baseGenerator';
import { Config } from '../common/constant';
import { ExampleParameter, ExampleValue } from '@autorest/testmodeler/dist/src/core/model';
import { GoExampleModel, GoMockTestDefinitionModel } from '../common/model';
import { GoHelper } from '../util/goHelper';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { elementByValueForParam } from '@autorest/go/dist/generator/helpers';
import { generateReturnsInfo, getAPIParametersSig, getClientParametersSig, getSchemaResponse } from '../util/codegenBridge';
import { isLROOperation, isMultiRespOperation, isPageableOperation } from '@autorest/go/dist/common/helpers';
import _ = require('lodash');
export class MockTestDataRender extends BaseDataRender {
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    public skipPropertyFunc = (exampleValue: ExampleValue): boolean => {
        // skip any null value
        if (exampleValue.rawValue === null) {
            return true;
        }
        return false;
    };
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    public replaceValueFunc = (rawValue: any, exampleValue: ExampleValue): any => {
        return rawValue;
    };

    public renderData(): void {
        const mockTest = this.context.codeModel.testModel.mockTest as GoMockTestDefinitionModel;
        for (const exampleGroup of mockTest.exampleGroups) {
            for (const example of exampleGroup.examples as GoExampleModel[]) {
                this.fillExampleOutput(example);
            }
        }
    }

    protected fillExampleOutput(example: GoExampleModel) {
        const op = example.operation;
        example.opName = op.language.go.name;
        if (isLROOperation(op as any)) {
            example.opName = 'Begin' + example.opName;
            example.isLRO = true;
            this.context.importManager.add('time');
            example.pollerType = example.operation.language.go.responseEnv.language.go.name;
        } else {
            example.isLRO = false;
        }
        example.isPageable = isPageableOperation(op as any);
        this.skipPropertyFunc = (exampleValue: ExampleValue): boolean => {
            // skip any null value
            if (exampleValue.rawValue === null) {
                return true;
            }
            return false;
        };
        this.replaceValueFunc = (rawValue: any): any => {
            return rawValue;
        };
        example.methodParametersOutput = this.toParametersOutput(getAPIParametersSig(op), example.methodParameters);
        example.clientParametersOutput = this.toParametersOutput(getClientParametersSig(example.operationGroup), example.clientParameters);
        example.returnInfo = generateReturnsInfo(op, 'op');
        const schemaResponse = getSchemaResponse(op as any);
        if (example.isPageable) {
            const valueName = op.extensions['x-ms-pageable'].itemName === undefined ? 'value' : op.extensions['x-ms-pageable'].itemName;
            for (const property of schemaResponse.schema['properties']) {
                if (property.serializedName === valueName) {
                    example.pageableItemName = property.language.go.name;
                    break;
                }
            }
            example.pageableType = example.operation.language.go.pageableType.name;
        }

        example.checkResponse =
            schemaResponse !== undefined &&
            schemaResponse.protocol.http.statusCodes[0] === '200' &&
            example.responses[schemaResponse.protocol.http.statusCodes[0]].body !== undefined;
        example.isMultiRespOperation = isMultiRespOperation(op);
        if (example.checkResponse && this.context.testConfig.getValue(Config.verifyResponse)) {
            this.context.importManager.add('encoding/json');
            this.context.importManager.add('reflect');
            this.skipPropertyFunc = (exampleValue: ExampleValue): boolean => {
                // mock-test will remove all NextLink param
                // skip any null value
                if (exampleValue.language?.go?.name === 'NextLink' || (exampleValue.rawValue === null && exampleValue.language?.go?.name !== 'ProvisioningState')) {
                    return true;
                }
                return false;
            };
            this.replaceValueFunc = (rawValue: any, exampleValue: ExampleValue): any => {
                // mock-test will change all ProvisioningState to Succeeded
                if (exampleValue.language?.go?.name === 'ProvisioningState') {
                    return 'Succeeded';
                }
                return rawValue;
            };
            example.responseOutput = this.exampleValueToString(example.responses[schemaResponse.protocol.http.statusCodes[0]].body, false);
            if (isMultiRespOperation(op)) {
                example.responseTypePointer = false;
                example.responseType = 'Value';
            } else {
                let responseEnv = op.language.go.responseEnv;
                if (isLROOperation(op)) {
                    responseEnv = op.language.go.finalResponseEnv;
                }
                if (responseEnv.language.go?.resultEnv.language.go?.resultField.schema.serialization?.xml?.name) {
                    example.responseTypePointer = !responseEnv.language.go?.resultEnv.language.go?.resultField.schema.language.go?.byValue;
                    example.responseType = responseEnv.language.go?.resultEnv.language.go?.resultField.schema.language.go?.name;
                    if (responseEnv.language.go?.resultEnv.language.go?.resultField.schema.isDiscriminator === true) {
                        example.responseType = `Get${example.responseType}()`;
                    }
                } else {
                    example.responseTypePointer = !responseEnv.language.go?.resultEnv.language.go?.resultField.language.go?.byValue;
                    example.responseType = responseEnv.language.go?.resultEnv.language.go?.resultField.language.go?.name;
                    if (responseEnv.language.go?.resultEnv.language.go?.resultField.isDiscriminator === true) {
                        example.responseType = `Get${example.responseType}()`;
                    }
                }
            }
        }
    }

    // get GO code of all parameters for one operation invoke
    protected toParametersOutput(paramsSig: Array<[string, string, Parameter | GroupProperty]>, exampleParameters: ExampleParameter[]): string {
        return paramsSig
            .map(([paramName, typeName, parameter]) => {
                if (parameter === undefined || parameter === null) {
                    return paramName;
                }
                return this.genParameterOutput(paramName, typeName, parameter, exampleParameters);
            })
            .join(',\n');
    }

    // get GO code of single parameter for one operation invoke
    protected genParameterOutput(paramName: string, paramType: string, parameter: Parameter | GroupProperty, exampleParameters: ExampleParameter[]): string {
        // get cooresponding example value of a parameter
        const findExampleParameter = (name: string, param: Parameter): string => {
            // isPtr need to consider three situation: 1) param is required 2) param is polymorphism 3) param is byValue
            const isPolymophismValue = param?.schema?.type === SchemaType.Object && (param.schema as ObjectSchema).discriminator?.property.isDiscriminator === true;
            const isPtr: boolean = isPolymophismValue || !(param.required || param.language.go.byValue === true);
            for (const methodParameter of exampleParameters) {
                if (this.getLanguageName(methodParameter.parameter) === name) {
                    // we should judge wheter a param or property is ptr or not from outside of exampleValueToString
                    return this.exampleValueToString(methodParameter.exampleValue, isPtr, elementByValueForParam(param));
                }
            }
            return this.getDefaultValue(param, isPtr, elementByValueForParam(param));
        };

        if ((parameter as GroupProperty).originalParameter) {
            const group = parameter as GroupProperty;
            const ptr = paramType.startsWith('*') ? '&' : '';
            let ret = `${ptr}${this.context.packageName + '.'}${this.getLanguageName(parameter.schema)}{`;
            let hasContent = false;
            for (const insideParameter of group.originalParameter) {
                if (insideParameter.implementation === ImplementationLocation.Client) {
                    // don't add globals to the per-method options struct
                    continue;
                }
                const insideOutput = findExampleParameter(this.getLanguageName(insideParameter), insideParameter);
                if (insideOutput) {
                    ret += `${this.getLanguageName(insideParameter)}: ${insideOutput},\n`;
                    hasContent = true;
                }
            }
            ret += '}';
            if (ptr.length > 0 && !hasContent) {
                ret = 'nil';
            }
            return ret;
        }
        return findExampleParameter(paramName, parameter);
    }

    protected getDefaultValue(param: Parameter | ExampleValue, isPtr: boolean, elemByVal = false) {
        if (isPtr) {
            return 'nil';
        } else {
            switch (param.schema.type) {
                case SchemaType.Char:
                case SchemaType.String:
                case SchemaType.Constant:
                    return '"<' + Helper.toKebabCase(this.getLanguageName(param)) + '>"';
                case SchemaType.Array: {
                    const elementIsPtr = param.schema.language.go.elementIsPtr && !elemByVal;
                    const elementPtr = elementIsPtr ? '*' : '';
                    let elementTypeName = this.getLanguageName((param.schema as ArraySchema).elementType);
                    const polymophismName = (param.schema as ArraySchema).elementType.language.go.discriminatorInterface;
                    if (polymophismName) {
                        elementTypeName = polymophismName;
                    }
                    return `[]${elementPtr}${GoHelper.addPackage(elementTypeName, this.context.packageName)}{}`;
                }
                case SchemaType.Dictionary: {
                    const elementPtr = param.schema.language.go.elementIsPtr ? '*' : '';
                    const elementTypeName = this.getLanguageName((param.schema as DictionarySchema).elementType);
                    return `map[string]${elementPtr}${GoHelper.addPackage(elementTypeName, this.context.packageName)}{}`;
                }
                case SchemaType.Boolean:
                    return 'false';
                case SchemaType.Integer:
                case SchemaType.Number:
                    return '0';
                case SchemaType.Object:
                    if (isPtr) {
                        return `&${this.context.packageName + '.'}${this.getLanguageName(param.schema)}{}`;
                    } else {
                        return `${this.context.packageName + '.'}${this.getLanguageName(param.schema)}{}`;
                    }
                default:
                    return '';
            }
        }
    }

    protected exampleValueToString(exampleValue: ExampleValue, isPtr: boolean, elemByVal = false, inArray = false): string {
        if (exampleValue === null || exampleValue === undefined || exampleValue.isNull) {
            return 'nil';
        }
        const ptr = isPtr ? '&' : '';
        if (exampleValue.schema?.type === SchemaType.Array) {
            const elementIsPtr = exampleValue.schema.language.go.elementIsPtr && !elemByVal;
            const elementPtr = elementIsPtr ? '*' : '';
            const schema = exampleValue.schema as ArraySchema;
            const elementIsPolymophism = schema.elementType.language.go.discriminatorInterface !== undefined;
            let elementTypeName = this.getLanguageName(schema.elementType);
            if (elementIsPolymophism) {
                elementTypeName = schema.elementType.language.go.discriminatorInterface;
            }
            if (exampleValue.elements === undefined) {
                const result = `${ptr}[]${elementPtr}${GoHelper.addPackage(elementTypeName, this.context.packageName)}{}`;
                return result;
            } else {
                // for pholymophism element, need to add type name, so pass false for inArray
                const result =
                    `${ptr}[]${elementPtr}${GoHelper.addPackage(elementTypeName, this.context.packageName)}{\n` +
                    exampleValue.elements.map((x) => this.exampleValueToString(x, elementIsPolymophism || elementIsPtr, false, elementIsPolymophism ? false : true)).join(',\n') +
                    '}';
                return result;
            }
        } else if (exampleValue.schema?.type === SchemaType.Object) {
            let output: string;
            if (inArray) {
                output = `{\n`;
            } else {
                output = `${ptr}${this.context.packageName + '.'}${this.getLanguageName(exampleValue.schema)}{\n`;
            }

            // object parents' properties will be aggregated to the child
            const parentsProps: ExampleValue[] = [];
            const additionalProps: ExampleValue[] = [];
            this.aggregateParentsProps(exampleValue.parentsValue, parentsProps, additionalProps);
            for (const parentsProp of parentsProps) {
                const isPolymophismValue =
                    parentsProp?.schema?.type === SchemaType.Object &&
                    ((parentsProp.schema as ObjectSchema).discriminatorValue !== undefined ||
                        (parentsProp.schema as ObjectSchema).discriminator?.property.isDiscriminator === true);
                output += `${this.getLanguageName(parentsProp)}: ${this.exampleValueToString(parentsProp, isPolymophismValue || !parentsProp.language.go?.byValue === true)},\n`;
            }
            // TODO: handle multiplue additionalProps
            for (const additionalProp of additionalProps) {
                output += `AdditionalProperties: ${this.exampleValueToString(additionalProp, false)},\n`;
            }
            for (const [_, value] of Object.entries(exampleValue.properties || {})) {
                if (this.skipPropertyFunc(value)) {
                    continue;
                }
                const isPolymophismValue =
                    value?.schema?.type === SchemaType.Object &&
                    ((value.schema as ObjectSchema).discriminatorValue !== undefined || (value.schema as ObjectSchema).discriminator?.property.isDiscriminator === true);
                output += `${this.getLanguageName(value)}: ${this.exampleValueToString(value, isPolymophismValue || !value.language.go?.byValue === true)},\n`;
            }
            output += '}';
            return output;
        } else if (exampleValue.schema?.type === SchemaType.Dictionary) {
            const elementPtr = exampleValue.schema.language.go.elementIsPtr && !elemByVal ? '*' : '';
            const elementTypeName = this.getLanguageName((exampleValue.schema as DictionarySchema).elementType);
            let output = `${ptr}map[string]${elementPtr}${GoHelper.addPackage(elementTypeName, this.context.packageName)}{\n`;
            for (const [key, value] of Object.entries(exampleValue.properties || {})) {
                output += `"${key}": ${this.exampleValueToString(value, exampleValue.schema.language.go.elementIsPtr)},\n`;
            }
            output += '}';
            return output;
        }

        const rawValue = this.getRawValue(exampleValue);
        if (rawValue === null) {
            return this.getDefaultValue(exampleValue, isPtr);
        }
        return this.rawValueToString(rawValue, exampleValue.schema, isPtr);
    }

    protected aggregateParentsProps(exampleValues: Record<string, ExampleValue>, parentsProps: ExampleValue[], additionalProps: ExampleValue[]) {
        for (const [_, value] of Object.entries(exampleValues || {})) {
            if (value.schema?.type === SchemaType.Object) {
                this.aggregateParentsProps(value.parentsValue, parentsProps, additionalProps);
                for (const [_, property] of Object.entries(value.properties)) {
                    if (this.skipPropertyFunc(property)) {
                        continue;
                    }
                    if (
                        parentsProps.filter((p) => {
                            return p.language.go.name === property.language.go.name;
                        }).length > 0
                    ) {
                        continue;
                    }
                    parentsProps.push(property);
                }
            } else if (value.schema?.type === SchemaType.Dictionary) {
                additionalProps.push(value);
            } else {
                parentsProps.push(value);
            }
        }
    }

    protected getRawValue(exampleValue: ExampleValue) {
        exampleValue.rawValue = this.replaceValueFunc(exampleValue.rawValue, exampleValue);
        return exampleValue.rawValue;
    }

    protected getStringValue(rawValue: string) {
        return Helper.quotedEscapeString(rawValue);
    }

    protected rawValueToString(rawValue: any, schema: Schema, isPtr: boolean): string {
        let ret = JSON.stringify(rawValue);
        const goType = this.getLanguageName(schema);
        if (schema.type === SchemaType.Choice) {
            ret = `${this.context.packageName}.${this.getLanguageName(schema)}("${rawValue}")`;
        } else if (schema.type === SchemaType.SealedChoice) {
            const choiceValue = Helper.findChoiceValue(schema as ChoiceSchema, rawValue);
            ret = this.context.packageName + '.' + this.getLanguageName(choiceValue);
        } else if (goType === 'string') {
            ret = this.getStringValue(rawValue);
        } else if (schema.type === SchemaType.ByteArray) {
            ret = `[]byte(${this.getStringValue(rawValue)})`;
        } else if (['int32', 'int64', 'float32', 'float64'].indexOf(goType) >= 0) {
            ret = `${Number(rawValue)}`;
        } else if (goType === 'time.Time') {
            this.context.importManager.add('time');
            const timeFormat = (schema as DateTimeSchema).format === 'date-time-rfc1123' ? 'time.RFC1123' : 'time.RFC3339Nano';
            ret = `func() time.Time { t, _ := time.Parse(${timeFormat}, "${rawValue}"); return t}()`;
        } else if (goType === 'map[string]interface{}') {
            ret = GoHelper.obejctToString(rawValue);
        } else if (goType === 'interface{}' && Array.isArray(rawValue)) {
            ret = GoHelper.arrayToString(rawValue);
        } else if (goType === 'interface{}' && typeof rawValue === 'object') {
            ret = GoHelper.obejctToString(rawValue);
        } else if (goType === 'interface{}' && _.isNumber(rawValue)) {
            ret = `float64(${rawValue})`;
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

            if ([SchemaType.Choice, SchemaType.SealedChoice].indexOf(schema.type) >= 0) {
                ret += '.ToPtr()';
            } else if (Object.prototype.hasOwnProperty.call(ptrConverts, goType)) {
                ret = `to.${ptrConverts[goType]}(${ret})`;
                this.context.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/azcore/to');
            } else {
                ret = '&' + ret;
            }
        }

        return ret;
    }

    protected getLanguageName(meta: any): string {
        return (meta as Metadata).language.go.name;
    }
}

export class MockTestCodeGenerator extends BaseCodeGenerator {
    public generateCode(extraParam: Record<string, unknown> = {}): void {
        this.renderAndWrite(this.context.codeModel.testModel.mockTest, 'mockTest.go.njk', `${this.getFilePrefix(Config.testFilePrefix)}mock_test.go`, extraParam);
    }
}
