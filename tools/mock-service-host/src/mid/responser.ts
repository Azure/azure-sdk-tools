import * as _ from 'lodash'
import * as fs from 'fs'
import * as oav from 'oav'
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
import {
    ExampleNotFound,
    ExampleNotMatch,
    HasChildResource,
    HttpStatusCode,
    NoParentResource,
    ResourceNotFound,
    WrongExampleResponse
} from '../common/errors'
import { InjectableTypes } from '../lib/injectableTypes'
import { JsonLoader } from 'oav/dist/lib/swagger/jsonLoader'
import { LiveRequest } from 'oav/dist/lib/liveValidation/operationValidator'
import { MockerCache, PayloadCache } from 'oav/dist/lib/generator/exampleCache'
import { Operation, SwaggerExample, SwaggerSpec } from 'oav/dist/lib/swagger/swaggerTypes'
import { ParsedUrlQuery } from 'querystring'
import { ResourcePool } from './resource'
import { TransformContext, getTransformContext } from 'oav/dist/lib/transform/context'
import { VirtualServerRequest, VirtualServerResponse } from './models'
import { applyGlobalTransformers, applySpecTransformers } from 'oav/dist/lib/transform/transformer'
import { discriminatorTransformer } from 'oav/dist/lib/transform/discriminatorTransformer'
import {
    getPath,
    getPureUrl,
    isManagementUrlLevel,
    isNullOrUndefined,
    logger,
    removeNullValueKey,
    replacePropertyValue
} from '../common/utils'
import { inject, injectable } from 'inversify'
import { inversifyGetInstance } from 'oav/dist/lib/inversifyUtils'
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
    private resourcePool: ResourcePool

    constructor(@inject(InjectableTypes.Config) private config: Config) {
        this.jsonLoader = inversifyGetInstance(JsonLoader, {})
        this.mockerCache = new MockerCache()
        this.payloadCache = new PayloadCache()
        this.swaggerMocker = new SwaggerMocker(this.jsonLoader, this.mockerCache, this.payloadCache)
        const schemaValidator = new AjvSchemaValidator(this.jsonLoader)
        this.transformContext = getTransformContext(this.jsonLoader, schemaValidator, [
            resolveNestedDefinitionTransformer,
            discriminatorTransformer
        ])
        this.initiateResourcePool()
    }

    public initiateResourcePool() {
        this.resourcePool = new ResourcePool(this.config.cascadeEnabled)
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

    private resolveValue(paramSpec: any, value: any): any {
        if (!paramSpec.type) {
            paramSpec = paramSpec.schema
        }
        switch (paramSpec.type) {
            case 'integer':
                return _.toInteger(value)
            case 'number':
                return _.toNumber(value)
            case 'boolean':
                return value === 'true' || value === true
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

    private async validateExampleResponse(
        liveValidator: oav.LiveValidator,
        url: string,
        method: string,
        statusCode: string,
        response: {
            body?: any
            headers?: { [headerName: string]: string }
        }
    ) {
        if (!response.headers) response.headers = {}
        if (!response.headers[Headers.ContentType]) {
            response.headers[Headers.ContentType] = 'application/json'
        }
        // exception will raise if is not valid example responses
        const validateResult = await liveValidator.validateLiveResponse(
            {
                statusCode: statusCode,
                headers: response.headers,
                body: response.body
            },
            {
                url: url,
                method: method
            }
        )
        if (!validateResult.isSuccessful) {
            throw new WrongExampleResponse(JSON.stringify(validateResult))
        }
    }

    public async loadSpecAndItem(
        operation: Operation,
        config: Config
    ): Promise<[string, SpecItem | undefined]> {
        const specFile = this.getSpecFileByOperation(operation, config)
        const spec = (await (this.jsonLoader.load(specFile) as unknown)) as SwaggerSpec
        applySpecTransformers(spec, this.transformContext)
        applyGlobalTransformers(this.transformContext)

        const specItem = ResponseGenerator.getSpecItem(spec, operation.operationId as string)
        return [specFile, specItem]
    }

    public async generate(
        res: VirtualServerResponse,
        liveValidator: oav.LiveValidator,
        operation: Operation,
        config: Config,
        liveRequest: LiveRequest,
        lroCallback: string | null
    ): Promise<[string, any]> {
        const [specFile, specItem] = await this.loadSpecAndItem(operation, config)
        if (!specItem) {
            throw Error(`operation ${operation.operationId} can't be found in ${specFile}`)
        }

        let example: SwaggerExample = {
            parameters: {},
            responses: {}
        } as SwaggerExample

        let statusCode: string = HttpStatusCode.OK.toString()
        let response: any = {}
        const exampleId = liveRequest.headers?.[Headers.ExampleId]
        if (exampleId) {
            example = this.loadExample(specFile, specItem, exampleId, liveRequest, lroCallback)
            this.validateRequestByExample(example, liveRequest, specItem)
            ;[statusCode, response] = this.chooseStatus(
                liveRequest.query,
                res,
                example.responses,
                lroCallback
            )
        } else {
            try {
                example = this.loadExample(specFile, specItem, exampleId, liveRequest, lroCallback)
                ;[statusCode, response] = this.chooseStatus(
                    liveRequest.query,
                    res,
                    example.responses,
                    lroCallback
                )
                await this.validateExampleResponse(
                    liveValidator,
                    liveRequest.url,
                    liveRequest.method,
                    statusCode,
                    response
                )
            } catch (err) {
                logger.error(`Failed to use example response, will mock response. Error:${err}`)
                example.responses[statusCode] = this.swaggerMocker.mockEachResponse(
                    statusCode,
                    {},
                    specItem
                ) as any
            }
            this.swaggerMocker.patchExampleResponses(example, liveRequest)
        }
        if (liveRequest.query?.[LRO_CALLBACK] === 'true') {
            this.setStatusToSuccess(example.responses[statusCode].body)
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
        return [statusCode, example.responses[statusCode]]
    }

    // The implementation of this function don't use jsonLoader since it removes all 'description' fields in example
    private loadExample(
        specFile: string,
        specItem: SpecItem,
        exampleId: string | undefined, // load any example if exampleId is undefined
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
        if (!allExamples) {
            throw new ExampleNotFound(exampleId)
        }

        if (exampleId === undefined) {
            exampleId = Object.keys(allExamples).sort()[0]
        } else if (!Object.prototype.hasOwnProperty.call(allExamples, exampleId)) {
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

    public async genStatefulResponse(
        req: VirtualServerRequest,
        res: VirtualServerResponse,
        profile: Record<string, any>,
        code: string,
        response: Record<string, any>
    ) {
        if (profile?.stateful) {
            const url: string = getPureUrl(req.url) as string
            const pathNames = getPath(url)
            // in stateful behaviour, GET and DELETE can only be called if resource/path exist
            if (
                ['GET', 'DELETE', 'PATCH'].indexOf(req.method.toUpperCase()) >= 0 &&
                isManagementUrlLevel(pathNames.length, url) &&
                !this.resourcePool.hasUrl(req)
            ) {
                throw new ResourceNotFound(url)
            }
        }

        const manipulateSucceed = this.resourcePool.updateResourcePool(req)
        if (profile?.stateful && !manipulateSucceed) {
            if (ResourcePool.isCreateMethod(req)) {
                throw new NoParentResource(req.url)
            } else {
                throw new HasChildResource(req.url)
            }
        } else {
            const isExampleResponse = !isNullOrUndefined(req.headers?.[Headers.ExampleId])
            let body = response.body
            if (typeof body === 'object') {
                // simplified paging
                // TODO: need to pair to spec to remove only the pager outer nextLink
                body = replacePropertyValue('nextLink', null, body)

                // simplified LRO
                body = replacePropertyValue('provisioningState', 'Succeeded', body)

                //set name
                const path = getPath(getPureUrl(req.url))
                if (!isExampleResponse) {
                    body = replacePropertyValue(
                        'name',
                        path[path.length - 1],
                        body,
                        (v) => {
                            return typeof v === 'string'
                        },
                        false
                    )
                }
            }
            res.set(code, body)
        }
    }

    private findResponse(
        responses: Record<string, any>,
        status: number,
        exactly = true
    ): [string, any] | undefined {
        if (exactly) {
            for (const code in responses) {
                if (status.toString() === code) {
                    return [status.toString(), responses[status.toString()]]
                }
            }
        } else {
            let nearest = undefined
            for (const code in responses) {
                if (
                    nearest === undefined ||
                    Math.abs(nearest - status) > Math.abs(parseInt(code) - status)
                ) {
                    nearest = parseInt(code)
                }
            }
            if (nearest) return [nearest.toString(), responses[nearest.toString()]]
        }
    }

    public chooseStatus(
        query: ParsedUrlQuery | undefined,
        res: VirtualServerResponse,
        exampleResponses: Record<string, any>,
        lroCallback: string | null = null
    ): [string, any] {
        let code: string, ret
        // for the lro operaion
        if (lroCallback !== null || query?.[LRO_CALLBACK] === 'true') {
            if (query?.[LRO_CALLBACK] === 'true') {
                // lro callback
                // if 202 response exist, need to return 200/201/204 response
                if (this.findResponse(exampleResponses, HttpStatusCode.ACCEPTED)) {
                    const result = this.findResponse(exampleResponses, HttpStatusCode.OK)
                    if (result) {
                        ;[code, ret] = result
                    } else {
                        const result = this.findResponse(
                            exampleResponses,
                            HttpStatusCode.NO_CONTENT
                        )
                        if (result) {
                            ;[code, ret] = result
                        } else {
                            const result = this.findResponse(
                                exampleResponses,
                                HttpStatusCode.CREATED
                            )
                            if (result) {
                                ;[code, ret] = result
                            } else {
                                // if no 200/201/204 response, throw exception
                                throw new WrongExampleResponse()
                            }
                        }
                    }
                } else {
                    // if 201 response exist, need to return 200 response
                    if (this.findResponse(exampleResponses, HttpStatusCode.CREATED)) {
                        const result = this.findResponse(exampleResponses, HttpStatusCode.OK)
                        if (result) {
                            ;[code, ret] = result
                        } else {
                            // if no 200 response, throw exception
                            throw new WrongExampleResponse()
                        }
                    } else {
                        // otherwise, need to return 200 response
                        const result = this.findResponse(exampleResponses, HttpStatusCode.OK)
                        if (result) {
                            ;[code, ret] = result
                        } else {
                            // if no 200 response, throw exception
                            throw new WrongExampleResponse()
                        }
                    }
                }
            } else {
                // lro first call, try to get 202 first
                const result = this.findResponse(exampleResponses, HttpStatusCode.ACCEPTED)
                if (result) {
                    ;[code, ret] = result
                    this.setLocationHeader(res, lroCallback)
                } else {
                    // if no 202 response, then try to get 201
                    const result = this.findResponse(exampleResponses, HttpStatusCode.CREATED)
                    if (result) {
                        ;[code, ret] = result
                        this.setAsyncHeader(res, lroCallback)
                    } else {
                        // last, get 200 related response
                        const result = this.findResponse(exampleResponses, HttpStatusCode.OK)
                        if (result) {
                            ;[code, ret] = result
                            this.setLocationHeader(res, lroCallback)
                        } else {
                            // if no 200 response, throw exception
                            throw new WrongExampleResponse()
                        }
                    }
                }
            }
        } else {
            // for normal operation, try to get a response
            const result = this.findResponse(exampleResponses, HttpStatusCode.OK, false)
            if (result) {
                ;[code, ret] = result
            } else {
                // if no response, throw exception
                throw new WrongExampleResponse()
            }
        }
        return [code, ret]
    }

    private setStatusToSuccess(ret: any) {
        // set status to succeed to stop polling
        if (ret) {
            try {
                ret['status'] = 'Succeeded'
            } catch (err) {
                // no object return, do nothing
            }
        } else {
            ret = { status: 'Succeeded' }
        }
        return ret
    }

    private setAsyncHeader(res: VirtualServerResponse, lroCallback: string | null) {
        // set Azure-AsyncOperation header
        res.setHeader('Azure-AsyncOperation', lroCallback)
        res.setHeader('Retry-After', 0)
    }

    private setLocationHeader(res: VirtualServerResponse, lroCallback: string | null) {
        // set location header
        res.setHeader('Location', lroCallback)
        res.setHeader('Retry-After', 0)
    }
}
