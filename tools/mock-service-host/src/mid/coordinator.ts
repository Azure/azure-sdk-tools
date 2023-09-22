import * as Constants from 'oav/dist/lib/util/constants'
import * as oav from 'oav'
import * as path from 'path'
import { AzureExtensions, LRO_CALLBACK } from '../common/constants'
import { Config } from '../common/config'
import {
    HttpStatusCode,
    IntentionalError,
    LroCallbackNotFound,
    ValidationFail
} from '../common/errors'
import { InjectableTypes } from '../lib/injectableTypes'
import { LiveValidationError } from 'oav/dist/lib/models'
import { Operation } from 'oav/dist/lib//swagger/swaggerTypes'
import { OperationMatch, OperationSearcher } from 'oav/dist/lib/liveValidation/operationSearcher'
import { ResponseGenerator } from './responser'
import { SpecRetriever } from '../lib/specRetriever'
import { ValidationRequest } from 'oav/dist/lib/liveValidation/operationValidator'
import { VirtualServerRequest, VirtualServerResponse } from './models'
import { getPath, getPureUrl, logger, replacePropertyValue } from '../common/utils'
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

    constructor(
        @inject(InjectableTypes.Config) private config: Config,
        @inject(InjectableTypes.SpecRetriever) private specRetriever: SpecRetriever,
        @inject(InjectableTypes.ResponseGenerator)
        private responseGenerator: ResponseGenerator
    ) {}

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
            let lroCallback: string | null = null
            if (result.operationMatch.operation[AzureExtensions.XMsLongRunningOperation]) {
                try {
                    lroCallback = await this.findLROGet(req, result.operationMatch.operation)
                } catch (err) {
                    if (err instanceof LroCallbackNotFound) {
                        // degrade to non-lro if 1) no callback operation can be found and 2) there is 200 responses
                        const [_, specItem] = await this.responseGenerator.loadSpecAndItem(
                            result.operationMatch.operation,
                            this.config
                        )
                        if (
                            !(HttpStatusCode.OK.toString() in (specItem?.content.responses || {}))
                        ) {
                            throw err
                        }
                    } else {
                        throw err
                    }
                }
            }
            const [statusCode, response] = await this.responseGenerator.generate(
                res,
                this.liveValidator,
                result.operationMatch.operation,
                this.config,
                liveRequest,
                lroCallback
            )
            if (profile?.alwaysError) {
                throw new IntentionalError()
            }
            await this.responseGenerator.genStatefulResponse(
                req,
                res,
                profile,
                statusCode,
                response
            )
        } else {
            const [statusCode, response] = this.handleSpecials(req, validationRequest)
            if (statusCode === undefined) {
                throw new ValidationFail(JSON.stringify(validateResult))
            } else {
                await this.responseGenerator.genStatefulResponse(
                    req,
                    res,
                    profile,
                    statusCode.toString(),
                    response
                )
            }
        }
    }

    public handleSpecials(
        req: VirtualServerRequest,
        validationRequest: ValidationRequest
    ): [HttpStatusCode | undefined, any] {
        if (validationRequest.providerNamespace === 'microsoft.unknown') {
            const path = getPath(getPureUrl(req.url))
            if (path.length === 2 && path[0].toLowerCase() === 'subscriptions') {
                // handle "/subscriptions/{subscriptionId}"
                return [
                    HttpStatusCode.OK,
                    {
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
                ]
            }
            if (path.length === 4 && path[2].toLowerCase() === 'resourcegroups') {
                // handle "/subscriptions/xxx/resourceGroups/xxx"
                return [
                    HttpStatusCode.OK,
                    {
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
                ]
            }
            if (path.length === 3 && path[2].toLowerCase() === 'locations') {
                return [
                    HttpStatusCode.OK,
                    {
                        body: replacePropertyValue(
                            '0000000-0000-0000-0000-000000000000',
                            path[1],
                            get_locations
                        )
                    }
                ]
            }
            if (path.length === 1 && path[0].toLowerCase() === 'tenants') {
                return [HttpStatusCode.OK, { body: get_tenants }]
            }
        }
        return [undefined, undefined]
    }

    async findLROGet(req: VirtualServerRequest, operation: Operation): Promise<string> {
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
                    const result = this.liveValidator.operationSearcher.search(validationRequest)
                    const finalState = HttpStatusCode.OK.toString()
                    if (
                        JSON.stringify(operation.responses?.[finalState]?.['schema']) ===
                        JSON.stringify(
                            result.operationMatch.operation?.responses?.[finalState]?.['schema']
                        )
                    )
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
