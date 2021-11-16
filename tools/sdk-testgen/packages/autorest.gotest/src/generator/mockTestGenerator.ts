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
import { isLROOperation, isPageableOperation } from '@autorest/go/dist/common/helpers';
export class MockTestDataRender extends BaseDataRender {
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
        example.methodParametersOutput = this.toParametersOutput(getAPIParametersSig(op), example.methodParameters);
        example.clientParametersOutput = this.toParametersOutput(getClientParametersSig(example.operationGroup), example.clientParameters);
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
                    const elementName = this.getLanguageName(responseSchema);
                    if (responseSchema.language.go.discriminatorInterface !== undefined) {
                        example.nonNilReturns.push(`${responseSchema.language.go.discriminatorInterface}.Get${elementName}().${variable}`);
                    } else {
                        example.nonNilReturns.push(`${elementName}.${variable}`);
                    }
                }
            }
        }
        example.checkResponse = example.nonNilReturns.length > 0;
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
            let isPtr: boolean = !param.required || isPolymophismValue === true;
            if (param.language.go.byValue && isPolymophismValue !== true) {
                isPtr = false;
            }
            for (const methodParameter of exampleParameters) {
                if (this.getLanguageName(methodParameter.parameter) === name) {
                    // go codegen use pointer for not required param
                    return this.exampleValueToString(methodParameter.exampleValue, isPtr, elementByValueForParam(methodParameter.parameter));
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

    protected exampleValueToString(exampleValue: ExampleValue, isPtr: boolean | undefined, elemByVal = false, inArray = false): string {
        if (exampleValue === null || exampleValue === undefined || exampleValue.isNull) {
            return 'nil';
        }
        const isPolymophismValue =
            exampleValue?.schema?.type === SchemaType.Object &&
            ((exampleValue.schema as ObjectSchema).discriminatorValue !== undefined || (exampleValue.schema as ObjectSchema).discriminator?.property.isDiscriminator === true);
        const ptr = (exampleValue.language?.go?.byValue && !isPolymophismValue) || isPtr === false ? '' : '&';
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
            for (const [_, parentValue] of Object.entries(exampleValue.parentsValue || {})) {
                const propertyName = parentValue.schema?.type === SchemaType.Dictionary ? 'AdditionalProperties' : this.getLanguageName(parentValue);
                output += `${propertyName}: ${this.exampleValueToString(parentValue, false)},\n`;
            }
            for (const [_, value] of Object.entries(exampleValue.properties || {})) {
                output += `${this.getLanguageName(value)}: ${this.exampleValueToString(value, undefined)},\n`;
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
            return this.getDefaultValue(exampleValue, isPtr === undefined ? !exampleValue.language.go.byValue : isPtr);
        }
        return this.rawValueToString(rawValue, exampleValue.schema, isPtr === undefined ? !exampleValue.language.go.byValue : isPtr);
    }

    protected getRawValue(exampleValue: ExampleValue) {
        return exampleValue.rawValue;
    }

    protected getStringValue(rawValue: string) {
        return Helper.quotedEscapeString(rawValue);
    }

    protected rawValueToString(rawValue: any, schema: Schema, isPtr: boolean): string {
        let ret = JSON.stringify(rawValue);
        if (rawValue !== null && rawValue !== undefined && Object.getPrototypeOf(rawValue) === Object.prototype) {
            ret = '`' + ret + '`';
        }
        const goType = this.getLanguageName(schema);
        if ([SchemaType.Choice, SchemaType.SealedChoice].indexOf(schema.type) >= 0) {
            const choiceValue = Helper.findChoiceValue(schema as ChoiceSchema, rawValue);
            ret = this.context.packageName + '.' + this.getLanguageName(choiceValue);
        } else if (schema.type === SchemaType.Constant || goType === 'string') {
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
                this.context.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/azcore/to');
            } else if ([SchemaType.Choice, SchemaType.SealedChoice].indexOf(schema.type) >= 0) {
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
