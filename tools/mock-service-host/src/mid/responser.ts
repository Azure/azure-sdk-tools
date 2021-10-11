import * as _ from 'lodash'
import * as fs from 'fs'
import * as path from 'path'
import { Config } from '../common/config'
import { ExampleNotFound, ExampleNotMatch } from '../common/errors'
import { Headers, ParameterType, SWAGGER_ENCODING, useREF } from '../common/constants'
import { JsonLoader } from 'oav/dist/lib/swagger/jsonLoader'
import { LiveRequest } from 'oav/dist/lib/liveValidation/operationValidator'
import { MockerCache, PayloadCache } from 'oav/dist/lib/generator/exampleCache'
import { Operation, SwaggerExample, SwaggerSpec } from 'oav/dist/lib/swagger/swaggerTypes'
import { injectable } from 'inversify'
import { inversifyGetInstance } from 'oav/dist/lib/inversifyUtils'
import { isNullOrUndefined } from '../common/utils'
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

    constructor() {
        this.jsonLoader = inversifyGetInstance(JsonLoader, {})
        this.mockerCache = new MockerCache()
        this.payloadCache = new PayloadCache()
        this.swaggerMocker = new SwaggerMocker(this.jsonLoader, this.mockerCache, this.payloadCache)
    }

    private getSpecItem(spec: any, operationId: string): SpecItem | undefined {
        const paths = spec.paths
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
        const specPathItems: string[] = specItem.path.split('/')
        const url = liveRequest.url.split('?')[0]
        const requestPathItems: string[] = url
            .substr(url.indexOf(':/') + 2)
            .split('/')
            .slice(1)
        const types: Record<string, ParameterType> = {}
        for (let paramSpec of specItem?.content?.parameters || []) {
            if (Object.prototype.hasOwnProperty.call(paramSpec, useREF)) {
                paramSpec = this.jsonLoader.resolveRefObj(paramSpec)
            }
            if (paramSpec.in === ParameterType.Path.toString()) {
                for (let i = 0; i < specPathItems.length; i++) {
                    const item = specPathItems[i]
                    if (
                        item.startsWith('{') &&
                        item.endsWith('}') &&
                        item.slice(1, -1) === paramSpec.name
                    ) {
                        parameters[paramSpec.name] = decodeURI(requestPathItems[i])
                    }
                }
                types[paramSpec.name] = ParameterType.Path
            } else if (paramSpec.in === ParameterType.Body.toString()) {
                parameters[paramSpec.name] = liveRequest.body
                types[paramSpec.name] = ParameterType.Body
            } else if (paramSpec.in === ParameterType.Query.toString()) {
                if (
                    liveRequest.query &&
                    Object.prototype.hasOwnProperty.call(liveRequest.query, paramSpec.name)
                ) {
                    parameters[paramSpec.name] = liveRequest.query[paramSpec.name]
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

    private validateRequestByExample(
        example: SwaggerExample,
        liveRequest: LiveRequest,
        specItem: SpecItem
    ) {
        const receivedExampleParameters = this.genExampleParameters(specItem, liveRequest)
        const requestParameters = receivedExampleParameters.exampleParameter
        const exampleParameters = example.parameters

        for (const [k, v] of Object.entries(requestParameters)) {
            if (exampleParameters[k] && !_.isEqual(exampleParameters[k], v)) {
                throw new ExampleNotMatch(
                    `${receivedExampleParameters.parameterTypes[k]} parameter ${k}=${JSON.stringify(
                        v
                    )} don't match example value ${JSON.stringify(exampleParameters[k])}`
                )
            }
        }
    }

    public async generate(operation: Operation, config: Config, liveRequest: LiveRequest) {
        const specFile = this.getSpecFileByOperation(operation, config)
        const spec = (await (this.jsonLoader.load(specFile) as unknown)) as SwaggerSpec
        const specItem = this.getSpecItem(spec, operation.operationId as string)
        if (!specItem) {
            throw Error(`operation ${operation.operationId} can't be found in ${specFile}`)
        }

        let example: SwaggerExample = {
            parameters: {},
            responses: {}
        } as SwaggerExample

        const exampleId = liveRequest.headers?.[Headers.ExampleId]
        if (exampleId) {
            example = this.loadExample(specFile, specItem, exampleId)
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
    private loadExample(specFile: string, specItem: SpecItem, exampleId: string): SwaggerExample {
        const rawSpec = JSON.parse(fs.readFileSync(specFile, SWAGGER_ENCODING))

        const allExamples = rawSpec.paths[specItem.path][specItem.methodName]['x-ms-examples']
        if (!allExamples || !Object.prototype.hasOwnProperty.call(allExamples, exampleId)) {
            throw new ExampleNotFound(exampleId)
        }

        const examplePath = allExamples[exampleId][useREF]

        return JSON.parse(
            fs.readFileSync(path.join(path.dirname(specFile), examplePath), SWAGGER_ENCODING)
        )
    }
}
