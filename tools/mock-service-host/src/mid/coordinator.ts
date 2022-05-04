import * as Constants from 'oav/dist/lib/util/constants'
import * as oav from 'oav'
import * as path from 'path'
import { AzureExtensions, Headers, LRO_CALLBACK } from '../common/constants'
import { Config } from '../common/config'
import {
    HasChildResource,
    HttpStatusCode,
    IntentionalError,
    LroCallbackNotFound,
    NoParentResource,
    ResourceNotFound,
    ValidationFail,
    WrongExampleResponse
} from '../common/errors'
import { InjectableTypes } from '../lib/injectableTypes'
import { LiveValidationError } from 'oav/dist/lib/models'
import { OperationMatch, OperationSearcher } from 'oav/dist/lib/liveValidation/operationSearcher'
import { ResourcePool } from './resource'
import { ResponseGenerator } from './responser'
import { SpecRetriever } from '../lib/specRetriever'
import { ValidationRequest } from 'oav/dist/lib/liveValidation/operationValidator'
import { VirtualServerRequest, VirtualServerResponse } from './models'
import {
    getPath,
    getPureUrl,
    isManagementUrlLevel,
    isNullOrUndefined,
    logger,
    replacePropertyValue
} from '../common/utils'
import { get_locations, get_tenants } from './specials'
import { inject, injectable } from 'inversify'
import _ from 'lodash'

export enum ValidatorStatus {
    NotInitialized = 'Validator not initialized',
    Initialized = 'Validator initialized',
    InitializationFailed = 'Validator initialization failure'
}

@injectable()
export class Coordinator {
    public liveValidator: oav.LiveValidator
    private statusValue = ValidatorStatus.NotInitialized
    private resourcePool: ResourcePool

    constructor(
        @inject(InjectableTypes.Config) private config: Config,
        @inject(InjectableTypes.SpecRetriever) private specRetriever: SpecRetriever,
        @inject(InjectableTypes.ResponseGenerator)
        private responseGenerator: ResponseGenerator
    ) {
        this.initiateResourcePool()
    }

    public initiateResourcePool() {
        this.resourcePool = new ResourcePool(this.config.cascadeEnabled)
    }

    private findResponse(
        responses: Record<string, any>,
        status: number,
        exactly = true
    ): [number, any] | undefined {
        if (exactly) {
            for (const code in responses) {
                if (status.toString() === code) {
                    return [status, _.cloneDeep(responses[status.toString()].body)]
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
            if (nearest) return [nearest, _.cloneDeep(responses[nearest.toString()].body)]
        }
    }

    private search(
        searcher: OperationSearcher,
        info: ValidationRequest
    ): {
        operationMatch: OperationMatch
        apiVersion: string
    } {
        const requestInfo = { ...info }
        const searchOperation = () => {
            const operations = searcher.getPotentialOperations(requestInfo)
            return operations
        }
        let potentialOperations = searchOperation()
        const firstReason = potentialOperations.reason

        if (potentialOperations?.matches.length === 0) {
            requestInfo.apiVersion = Constants.unknownApiVersion
            potentialOperations = searchOperation()
        }

        if (potentialOperations.matches.length === 0) {
            throw firstReason ?? potentialOperations.reason
        }

        return {
            operationMatch: potentialOperations.matches.slice(-1)[0],
            apiVersion: potentialOperations.apiVersion
        }
    }

    public get Validator(): oav.LiveValidator {
        return this.liveValidator
    }

    public get ValidatorStatus(): ValidatorStatus {
        return this.statusValue
    }

    public async initialize(): Promise<void> {
        if (this.liveValidator) {
            return
        }

        try {
            await this.specRetriever.retrieveSpecs()
        } catch (err) {
            logger.error(`Validator: unable to refresh the Validator. Error:${err}`)
            return
        }

        const options = {
            git: {
                url: this.config.specRetrievalGitUrl,
                shouldClone: false
            },
            swaggerPathsPattern: this.config.validationPathsPattern,
            excludedSwaggerPathsPattern: this.config.excludedValidationPathsPattern,
            directory: path.resolve(this.config.specRetrievalLocalRelativePath),
            isPathCaseSensitive: false
        }
        logger.info(`validator is initializing with options ${JSON.stringify(options, null, 4)}`)
        this.liveValidator = new oav.LiveValidator(options)
        await this.liveValidator.initialize()
        this.statusValue = ValidatorStatus.Initialized
        logger.info('validator initialized')
    }

    public async generateResponse(
        req: VirtualServerRequest,
        res: VirtualServerResponse,
        profile: Record<string, any>
    ) {
        const fullUrl = req.protocol + '://' + req.headers?.host + req.url
        const liveRequest = {
            url: fullUrl,
            method: req.method,
            headers: req.headers as any,
            query: req.query as any,
            body: req.body
        }

        const validationRequest = this.liveValidator.parseValidationRequest(
            liveRequest.url,
            liveRequest.method,
            ''
        )
        const validateResult = await this.validate(liveRequest)

        if (
            validateResult.isSuccessful ||
            validateResult.runtimeException?.code ===
                Constants.ErrorCodes.MultipleOperationsFound.name
        ) {
            const result = this.search(this.liveValidator.operationSearcher, validationRequest)
            const lroCallback = result.operationMatch.operation[
                AzureExtensions.XMsLongRunningOperation
            ]
                ? await this.findLROGet(req)
                : null
            const example = await this.responseGenerator.generate(
                result.operationMatch.operation,
                this.config,
                liveRequest,
                lroCallback
            )
            if (profile?.alwaysError) {
                throw new IntentionalError()
            }
            await this.genStatefulResponse(req, res, example.responses, profile, lroCallback)
        } else {
            const exampleResponse = this.handleSpecials(req, validationRequest)
            if (exampleResponse === undefined) {
                throw new ValidationFail(JSON.stringify(validateResult))
            } else {
                await this.genStatefulResponse(req, res, exampleResponse, profile)
            }
        }
    }

    public handleSpecials(
        req: VirtualServerRequest,
        validationRequest: ValidationRequest
    ): Record<string, any> | undefined {
        if (validationRequest.providerNamespace === 'microsoft.unknown') {
            const path = getPath(getPureUrl(req.url))
            if (path.length === 2 && path[0].toLowerCase() === 'subscriptions') {
                // handle "/subscriptions/{subscriptionId}"
                return {
                    [HttpStatusCode.OK]: {
                        body: {
                            id: `/subscriptions/${path[1]}`,
                            authorizationSource: 'RoleBased',
                            managedByTenants: [],
                            subscriptionId: `${path[1]}`,
                            tenantId: '0000000-0000-0000-0000-000000000000',
                            displayName: 'Name of the subscription',
                            state: 'Enabled',
                            subscriptionPolicies: {
                                locationPlacementId: 'Internal_2014-09-01',
                                quotaId: 'Internal_2014-09-01',
                                spendingLimit: 'Off'
                            }
                        }
                    }
                }
            }
            if (path.length === 4 && path[2].toLowerCase() === 'resourcegroups') {
                // handle "/subscriptions/xxx/resourceGroups/xxx"
                return {
                    [HttpStatusCode.OK]: {
                        body: {
                            id: getPureUrl(req.url),
                            location: 'eastus',
                            managedBy: null,
                            name: path[3],
                            properties: {
                                provisioningState: 'Succeeded'
                            },
                            tags: {},
                            type: 'Microsoft.Resources/resourceGroups'
                        }
                    }
                }
            }
            if (path.length === 3 && path[2].toLowerCase() === 'locations') {
                return {
                    [HttpStatusCode.OK]: {
                        body: replacePropertyValue(
                            '0000000-0000-0000-0000-000000000000',
                            path[1],
                            get_locations
                        )
                    }
                }
            }
            if (path.length === 1 && path[0].toLowerCase() === 'tenants') {
                return { [HttpStatusCode.OK]: { body: get_tenants } }
            }
        }
        return undefined
    }

    public async genStatefulResponse(
        req: VirtualServerRequest,
        res: VirtualServerResponse,
        exampleResponses: Record<string, any>,
        profile: Record<string, any>,
        lroCallback: string | null = null
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
            let code, ret
            // for the lro operaion
            if (lroCallback !== null || req.query?.[LRO_CALLBACK] === 'true') {
                if (req.query?.[LRO_CALLBACK] === 'true') {
                    // lro callback
                    // if 202 response exist, need to return 200/201/204 response
                    if (this.findResponse(exampleResponses, HttpStatusCode.ACCEPTED)) {
                        const result = this.findResponse(exampleResponses, HttpStatusCode.OK)
                        if (result) {
                            ;[code, ret] = result
                            ret = this.setStatusToSuccess(ret)
                        } else {
                            const result = this.findResponse(
                                exampleResponses,
                                HttpStatusCode.NO_CONTENT
                            )
                            if (result) {
                                ;[code, ret] = result
                                ret = this.setStatusToSuccess(ret)
                            } else {
                                const result = this.findResponse(
                                    exampleResponses,
                                    HttpStatusCode.CREATED
                                )
                                if (result) {
                                    ;[code, ret] = result
                                    ret = this.setStatusToSuccess(ret)
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
                                ret = this.setStatusToSuccess(ret)
                            } else {
                                // if no 200 response, throw exception
                                throw new WrongExampleResponse()
                            }
                        } else {
                            // otherwise, need to return 200 response
                            const result = this.findResponse(exampleResponses, HttpStatusCode.OK)
                            if (result) {
                                ;[code, ret] = result
                                ret = this.setStatusToSuccess(ret)
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
                                this.setAsyncHeader(res, lroCallback)
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

            const isExampleResponse = !isNullOrUndefined(req.headers?.[Headers.ExampleId])
            if (typeof ret === 'object') {
                // simplified paging
                // TODO: need to pair to spec to remove only the pager outer nextLink
                ret = replacePropertyValue('nextLink', null, ret)

                // simplified LRO
                ret = replacePropertyValue('provisioningState', 'Succeeded', ret)

                //set name
                const path = getPath(getPureUrl(req.url))
                if (!isExampleResponse) {
                    ret = replacePropertyValue(
                        'name',
                        path[path.length - 1],
                        ret,
                        (v) => {
                            return typeof v === 'string'
                        },
                        false
                    )
                }
            }

            res.set(code, ret)
        }
    }

    private setStatusToSuccess(ret: any) {
        // set status to succeed to stop polling
        if (ret) {
            ret.status = 'Succeeded'
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

    async findLROGet(req: VirtualServerRequest): Promise<string> {
        const [uri, query] = `${req.url}&${LRO_CALLBACK}=true`.split('?')
        const uriPath = uri.split('/')
        const oriLen = uriPath.length
        let firstloop = true
        while (uriPath.length > 0) {
            // if trackback for two part without found, then throw `no cooresponding get method` error
            if (oriLen - uriPath.length > 4) {
                break
            }
            if (firstloop || uriPath.length % 2 === 1) {
                let hostAndPort = req.headers?.host as string
                if (hostAndPort.indexOf(':') < 0) {
                    hostAndPort = `${hostAndPort}:${req.localPort}`
                }
                const testingUrl = `${req.protocol}://${hostAndPort}${uriPath.join('/')}?${query}`
                try {
                    const validationRequest = this.liveValidator.parseValidationRequest(
                        testingUrl,
                        'GET',
                        req.headers?.[AzureExtensions.XMsCorrelationRequestId] || ''
                    )
                    this.liveValidator.operationSearcher.search(validationRequest)
                    return testingUrl
                } catch (error) {
                    if (
                        error instanceof LiveValidationError &&
                        error?.code === Constants.ErrorCodes.MultipleOperationsFound.name
                    ) {
                        return testingUrl
                    }
                    console.info(error) // don't has 'GET' verb for this url
                }
            }
            uriPath.splice(-1)
            firstloop = false
        }
        throw new LroCallbackNotFound(`Lro operation: ${req.method} ${req.url}`)
    }

    private async validate(liveRequest: oav.LiveRequest) {
        return this.liveValidator.validateLiveRequest(liveRequest)
    }
}
