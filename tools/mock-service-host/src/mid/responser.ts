import * as _ from 'lodash'
import * as fs from 'fs'
import * as path from 'path'
import { AjvSchemaValidator } from 'oav/dist/lib/swaggerValidator/ajvSchemaValidator'
import {
    AzureExtensions,
    Headers,
    LRO_CALLBACK,
    ParameterType,
    SWAGGER_ENCODING,
    useREF
} from '../common/constants'
import { Config } from '../common/config'
import { ExampleNotFound, ExampleNotMatch } from '../common/errors'
import { JsonLoader } from 'oav/dist/lib/swagger/jsonLoader'
import { LiveRequest } from 'oav/dist/lib/liveValidation/operationValidator'
import { MockerCache, PayloadCache } from 'oav/dist/lib/generator/exampleCache'
import { Operation, SwaggerExample, SwaggerSpec } from 'oav/dist/lib/swagger/swaggerTypes'
import { TransformContext, getTransformContext } from 'oav/dist/lib/transform/context'
import { applyGlobalTransformers, applySpecTransformers } from 'oav/dist/lib/transform/transformer'
import { discriminatorTransformer } from 'oav/dist/lib/transform/discriminatorTransformer'
import { injectable } from 'inversify'
import { inversifyGetInstance } from 'oav/dist/lib/inversifyUtils'
import { isNullOrUndefined, removeNullValueKey } from '../common/utils'
import { resolveNestedDefinitionTransformer } from 'oav/dist/lib/transform/resolveNestedDefinitionTransformer'
import SwaggerMocker from './oav/swaggerMocker'

export interface SwaggerExampleParameter {
    'api-version': string
    [parameterName: string]: any
}

export class SpecItem {
    public content: Operation
    public path: string
    public methodName: string
}

@injectable()
export class ResponseGenerator {
    private jsonLoader: JsonLoader
    private mockerCache: MockerCache
    private payloadCache: PayloadCache
    private swaggerMocker: SwaggerMocker
    public readonly transformContext: TransformContext
    private lroExamplesMap: Map<string, any> = new Map<string, any>()

    constructor() {
        this.jsonLoader = inversifyGetInstance(JsonLoader, {})
        this.mockerCache = new MockerCache()
        this.payloadCache = new PayloadCache()
        this.swaggerMocker = new SwaggerMocker(this.jsonLoader, this.mockerCache, this.payloadCache)
        const schemaValidator = new AjvSchemaValidator(this.jsonLoader)
        this.transformContext = getTransformContext(this.jsonLoader, schemaValidator, [
            resolveNestedDefinitionTransformer,
            discriminatorTransformer
        ])
    }

    public static getSpecItem(spec: any, operationId: string): SpecItem | undefined {
        let paths = spec.paths || {}
        if (spec[AzureExtensions.XMsPaths]) {
            paths = { ...paths, ...spec[AzureExtensions.XMsPaths] }
        }
        for (const pathName of Object.keys(paths)) {
            for (const methodName of Object.keys(paths[pathName])) {
                if (paths[pathName][methodName].operationId === operationId) {
                    const ret = {
                        path: pathName,
                        methodName,
                        content: paths[pathName][methodName]
                    }

                    if (isNullOrUndefined(ret.content.parameters)) {
                        ret.content.parameters = []
                    }
                    if (paths[pathName].parameters) {
                        ret.content.parameters.push(...paths[pathName].parameters)
                    }
                    return ret
                }
            }
        }
        return undefined
    }

    public getSpecFileByOperation(operation: Operation, config: Config): string {
        return path.join(
            path.resolve(config.specRetrievalLocalRelativePath),
            operation._path._spec._filePath
        )
    }

    private genExampleParameters(
        specItem: any,
        liveRequest: LiveRequest
    ): {
        exampleParameter: SwaggerExampleParameter
        parameterTypes: Record<string, ParameterType>
    } {
        const parameters: Record<string, any> = {}
        // replace all the param placeholder in path with regex group annotation
        const specPathRegex = new RegExp(
            specItem.path
                .split('/')
                .map((v: string) => {
                    if (v.startsWith('{') && v.endsWith('}')) {
                        return `(?<${v.slice(1, -1)}>.*)`
                    } else {
                        return v
                    }
                })
                .join('\\/')
        )
        // remove request url query string, http:// and host:port
        let url = liveRequest.url.split('?')[0]
        url =
            '/' +
            url
                .substr(url.indexOf(':/') + 3)
                .split('/')
                .slice(1)
                .join('/')
        // exec regex to pair all the path param with value
        const urlMappingResult = specPathRegex.exec(url)
        const types: Record<string, ParameterType> = {}
        for (let paramSpec of specItem?.content?.parameters || []) {
            if (Object.prototype.hasOwnProperty.call(paramSpec, useREF)) {
                paramSpec = this.jsonLoader.resolveRefObj(paramSpec)
            }
            if (paramSpec.in === ParameterType.Path.toString()) {
                // use regex group to get path param value
                if (
                    urlMappingResult !== null &&
                    urlMappingResult.groups &&
                    urlMappingResult.groups[paramSpec.name]
                ) {
                    parameters[paramSpec.name] = this.resolveValue(
                        paramSpec,
                        decodeURIComponent(urlMappingResult.groups[paramSpec.name])
                    )
                    types[paramSpec.name] = ParameterType.Path
                }
            } else if (paramSpec.in === ParameterType.Body.toString()) {
                parameters[paramSpec.name] = liveRequest.body
                types[paramSpec.name] = ParameterType.Body
            } else if (paramSpec.in === ParameterType.Query.toString()) {
                if (
                    liveRequest.query &&
                    Object.prototype.hasOwnProperty.call(liveRequest.query, paramSpec.name)
                ) {
                    parameters[paramSpec.name] = this.resolveValue(
                        paramSpec,
                        liveRequest.query[paramSpec.name]
                    )
                    types[paramSpec.name] = ParameterType.Query
                }
            } else if (paramSpec.in === ParameterType.Header.toString()) {
                if (
                    liveRequest.headers &&
                    Object.prototype.hasOwnProperty.call(liveRequest.headers, paramSpec.name)
                ) {
                    parameters[paramSpec.name] = liveRequest.headers[paramSpec.name]
                    types[paramSpec.name] = ParameterType.Header
                }
            }
        }
        return { exampleParameter: parameters as SwaggerExampleParameter, parameterTypes: types }
    }

    private resolveValue(paramSpec: any, value: string | string[]): any {
        if (!paramSpec.type) {
            paramSpec = paramSpec.schema
        }
        switch (paramSpec.type) {
            case 'integer':
                return _.toInteger(value)
            case 'number':
                return _.toNumber(value)
            case 'boolean':
                return value === 'true'
            case 'array':
                return _.map(value, (s) => {
                    return this.resolveValue(paramSpec.items, s)
                })
            default:
                return value
        }
    }

    private validateRequestByExample(
        example: SwaggerExample,
        liveRequest: LiveRequest,
        specItem: SpecItem
    ) {
        const receivedExampleParameters = this.genExampleParameters(specItem, liveRequest)
        const requestParameters = receivedExampleParameters.exampleParameter
        const exampleParameters = example.parameters

        for (const [k, v] of Object.entries(requestParameters)) {
            if (
                exampleParameters[k] &&
                !_.isEqualWith(
                    removeNullValueKey(exampleParameters[k]),
                    removeNullValueKey(v),
                    this.customizerIsEqual
                )
            ) {
                throw new ExampleNotMatch(
                    `${receivedExampleParameters.parameterTypes[k]} parameter ${k}=${JSON.stringify(
                        v
                    )} don't match example value ${JSON.stringify(exampleParameters[k])}`
                )
            }
        }
    }

    private customizerIsEqual(objValue: any, othValue: any): boolean | undefined {
        // rfc3339 timestamp comparison
        const rfc3339Regex = /^((?:(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2}:\d{2}(?:\.\d+)?))(Z|[+-]\d{2}:\d{2})?)$/g
        if (
            _.isString(objValue) &&
            objValue.match(rfc3339Regex) &&
            _.isString(othValue) &&
            othValue.match(rfc3339Regex)
        ) {
            return Date.parse(objValue) === Date.parse(othValue)
        }
    }

    public async generate(
        operation: Operation,
        config: Config,
        liveRequest: LiveRequest,
        lroCallback: string | null
    ) {
        const specFile = this.getSpecFileByOperation(operation, config)
        const spec = (await (this.jsonLoader.load(specFile) as unknown)) as SwaggerSpec
        applySpecTransformers(spec, this.transformContext)
        applyGlobalTransformers(this.transformContext)

        const specItem = ResponseGenerator.getSpecItem(spec, operation.operationId as string)
        if (!specItem) {
            throw Error(`operation ${operation.operationId} can't be found in ${specFile}`)
        }

        let example: SwaggerExample = {
            parameters: {},
            responses: {}
        } as SwaggerExample

        const exampleId = liveRequest.headers?.[Headers.ExampleId]
        if (exampleId) {
            example = this.loadExample(specFile, specItem, exampleId, liveRequest, lroCallback)
            this.validateRequestByExample(example, liveRequest, specItem)
        } else {
            this.swaggerMocker.mockForExample(example, specItem, spec, 'unknown', liveRequest)
        }
        if (config.enableExampleGeneration) {
            const params = this.genExampleParameters(specItem, liveRequest)
            example['parameters'] = params.exampleParameter

            let genExamplePath = ''
            for (let exampleIndex = 1; ; exampleIndex++) {
                genExamplePath = path.join(
                    specFile,
                    '..',
                    config.exampleGenerationFolder,
                    `${specItem.content.operationId}_${exampleIndex}_gen.json`
                )
                if (!fs.existsSync(genExamplePath)) break
            }
            const exampleFolder = path.dirname(genExamplePath)
            if (!fs.existsSync(exampleFolder)) {
                fs.mkdirSync(exampleFolder)
            }
            fs.writeFileSync(genExamplePath, JSON.stringify(example, null, 2), 'utf8')
        }
        return example
    }

    // The implementation of this function don't use jsonLoader since it removes all 'description' fields in example
    private loadExample(
        specFile: string,
        specItem: SpecItem,
        exampleId: string,
        liveRequest: LiveRequest,
        lroCallback: string | null
    ): SwaggerExample {
        const rawSpec = JSON.parse(fs.readFileSync(specFile, SWAGGER_ENCODING))
        let allExamples
        if (rawSpec.paths[specItem.path]) {
            allExamples = _.mapKeys(
                rawSpec.paths[specItem.path][specItem.methodName][AzureExtensions.XMsExamples],
                (_, k) => k.trim()
            )
        } else {
            allExamples = _.mapKeys(
                rawSpec['x-ms-paths'][specItem.path][specItem.methodName][
                    AzureExtensions.XMsExamples
                ],
                (_, k) => k.trim()
            )
        }
        if (this.lroExamplesMap.has(liveRequest.url)) {
            allExamples = { ...this.lroExamplesMap.get(liveRequest.url), ...allExamples }
        } else {
            const urlWithCallback = `${liveRequest.url}&${LRO_CALLBACK}=true`
            if (this.lroExamplesMap.has(urlWithCallback)) {
                allExamples = { ...this.lroExamplesMap.get(urlWithCallback), ...allExamples }
            }
        }

        if (!allExamples || !Object.prototype.hasOwnProperty.call(allExamples, exampleId)) {
            throw new ExampleNotFound(exampleId)
        }

        if (lroCallback) {
            this.lroExamplesMap.set(lroCallback, allExamples)
        }

        const examplePath = allExamples[exampleId][useREF]

        return JSON.parse(
            fs.readFileSync(path.join(path.dirname(specFile), examplePath), SWAGGER_ENCODING)
        )
    }
}
