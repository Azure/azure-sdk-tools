import 'reflect-metadata' // Must be imported exactly once for Inversify

/* eslint-disable @typescript-eslint/no-var-requires */
import * as assert from 'assert'
import * as oav from 'oav'
import * as path from 'path'
import { Coordinator } from '../../src/mid/coordinator'
import {
    ExampleNotFound,
    ExampleNotMatch,
    HttpStatusCode,
    IntentionalError,
    LroCallbackNotFound,
    NoParentResource,
    ResourceNotFound,
    ValidationFail
} from '../../src/common/errors'
import { RequestResponsePair } from 'oav/dist/lib/liveValidation/liveValidator'
import { ResponseGenerator } from '../../src/mid/responser'
import { SpecRetrieverFilesystem } from '../../src/lib/specRetrieverFilesystem'
import { config } from '../../src/common/index'
import {
    createLiveRequestForCreateApiManagementService,
    createLiveRequestForCreateRG,
    createLiveRequestForDeleteApiManagementService,
    genFakeResponses,
    mockDefaultResponse,
    mockRequest
} from '../tools'

const statefulProfile = {
    stateful: true
}
const statelessProfile = {
    stateful: false
}
const alwaysErrorProfile = {
    alwaysError: 500
}

describe('initialize validator:', () => {
    const specRetriever = new SpecRetrieverFilesystem(config)
    const coordinator = new Coordinator(config, specRetriever, new ResponseGenerator())

    beforeAll(async () => {
        await coordinator.initialize()
    })

    it('should initialize with swagger directory', async () => {
        const cache = coordinator.Validator.operationSearcher.cache
        assert.strictEqual(true, cache.has('microsoft.apimanagement'))
        assert.strictEqual(true, cache.has('microsoft.media'))
    })
})

describe('generateResponse()', () => {
    const specRetriever = new SpecRetrieverFilesystem(config)
    const coordinator = new Coordinator(config, specRetriever, new ResponseGenerator())
    beforeAll(async () => {
        await coordinator.initialize()
    })

    beforeEach(async () => {
        coordinator.initiateResourcePool()
        jest.restoreAllMocks()
    })

    it('validate GET input', async () => {
        const fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        await coordinator.generateResponse(request, response, statelessProfile)
        assert.strictEqual(response.statusCode, pair.liveResponse.statusCode)
        pair.liveResponse.body = response.body
        pair.liveResponse.headers = response.headers

        await coordinator.Validator.validateLiveRequestResponse(pair)
    })

    it('alwaysError: return 500 even for a valid_input', async () => {
        const fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        await expect(
            coordinator.generateResponse(request, response, alwaysErrorProfile)
        ).rejects.toThrow(IntentionalError)
    })

    it('validate DELETE input', async () => {
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_delete.json'
        )
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        await coordinator.generateResponse(request, response, statelessProfile)
        assert.strictEqual(response.statusCode, pair.liveResponse.statusCode)
        expect(response).toMatchSnapshot()
        pair.liveResponse.body = response.body
        pair.liveResponse.headers = response.headers
        pair.liveResponse.headers['Content-Type'] = 'application/json'
        const result = await coordinator.Validator.validateLiveRequestResponse(pair)
        assert.ok(result.requestValidationResult.isSuccessful)
        assert.ok(result.responseValidationResult.isSuccessful)
    })

    it('invalidate input', async () => {
        const fileName = path.join(__dirname, '..', 'testData', 'payloads', 'invalidUrl_input.json')
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        await expect(
            coordinator.generateResponse(request, response, statelessProfile)
        ).rejects.toThrow(ValidationFail)
    })

    it('special rule of GET resourceGroup', async () => {
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'special_resourcegroup.json'
        )
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        await coordinator.generateResponse(request, response, statelessProfile)
        assert.strictEqual(response.statusCode, pair.liveResponse.statusCode)
        expect(response).toMatchSnapshot()
    })

    it('special rule of GET locations', async () => {
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'special_resourcegroup.json'
        )
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        await coordinator.generateResponse(request, response, statelessProfile)
        assert.strictEqual(response.statusCode, pair.liveResponse.statusCode)
        expect(response).toMatchSnapshot()
        if (!pair.liveResponse.headers) pair.liveResponse.headers = {}
        const result = await coordinator.Validator.validateLiveRequestResponse(pair)
        assert.strictEqual(result.requestValidationResult.isSuccessful, undefined) // since this is a special URI not handled by oav
    })

    it('findLROGet', async () => {
        jest.spyOn(coordinator.liveValidator, 'parseValidationRequest').mockImplementation(
            (
                requestUrl: string,
                requestMethod: string | undefined | null,
                correlationId: string
            ) => {
                const getUrls = [
                    'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type',
                    'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType',
                    'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2',
                    'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2/myType2',
                    'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2/myType2/start',
                    'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2/myType2/Type3'
                ]
                if (getUrls.indexOf(requestUrl.split('?')[0]) < 0) {
                    throw new Error('No operation')
                }
                return {
                    providerNamespace: '',
                    resourceType: '',
                    apiVersion: '',
                    requestMethod: 'get',
                    host: '',
                    pathStr: '',
                    correlationId: correlationId,
                    requestUrl: requestUrl
                }
            }
        )
        jest.spyOn(coordinator.liveValidator.operationSearcher, 'search').mockImplementation(
            (_) => {
                return {
                    operationMatch: 'fake',
                    apiVersion: 'fake'
                } as any
            }
        )

        const liveRequest = {
            protocol: 'https',
            url:
                '/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2/myType2/Type3/myType3/start?api-version=20210701',
            method: 'POST',
            headers: {
                host: 'localhost'
            },
            localPort: 8443
        }
        assert.strictEqual(
            await coordinator.findLROGet(liveRequest),
            'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2/myType2?api-version=20210701&lro-callback=true'
        )

        liveRequest.url =
            '/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2/myType2?api-version=20210701'
        assert.strictEqual(
            await coordinator.findLROGet(liveRequest),
            'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2/myType2?api-version=20210701&lro-callback=true'
        )

        liveRequest.url =
            '/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2/myType2/stop?api-version=20210701'
        assert.strictEqual(
            await coordinator.findLROGet(liveRequest),
            'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/Type2/myType2?api-version=20210701&lro-callback=true'
        )

        liveRequest.url =
            '/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType/new?api-version=20210701'
        assert.strictEqual(
            await coordinator.findLROGet(liveRequest),
            'https://localhost/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type/myType?api-version=20210701&lro-callback=true'
        )

        liveRequest.url =
            '/subscriptions/xxx/resourceGroups/xx/providers/Microsoft.Mock/Type2/myType/new?api-version=20210701'
        expect(coordinator.findLROGet(liveRequest)).rejects.toThrow(LroCallbackNotFound)
    })
})

describe('genStatefulResponse()', () => {
    const specRetriever = new SpecRetrieverFilesystem(config)
    const coordinator = new Coordinator(config, specRetriever, new ResponseGenerator())

    beforeAll(async () => {
        await coordinator.initialize()
        config.cascadeEnabled = true
    })

    beforeEach(async () => {
        coordinator.initiateResourcePool()
    })

    it('stateful: return 404 for GET even it is a valid_input', async () => {
        const fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        const pair: RequestResponsePair = require(fileName)

        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        //await generateResponse(validator, request, response, statelessProfile)

        expect(
            coordinator.genStatefulResponse(
                request,
                response,
                { [response.statusCode]: response.body },
                statefulProfile
            )
        ).rejects.toThrow(ResourceNotFound)
    })

    it('stateful: return 404 for DELETE even it is a valid_input', async () => {
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_delete.json'
        )
        const pair: RequestResponsePair = require(fileName)

        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        //await generateResponse(validator, request, response, statelessProfile)

        expect(
            coordinator.genStatefulResponse(request, response, genFakeResponses(), statefulProfile)
        ).rejects.toThrow(ResourceNotFound)
    })

    it('stateful create->read->delete', async () => {
        const response = mockDefaultResponse()

        //create rg and service before test
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForCreateRG()),
            response,
            genFakeResponses(),
            statefulProfile
        )
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForCreateApiManagementService()),
            response,
            genFakeResponses(),
            statefulProfile
        )

        // create
        let fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input_create.json')
        let pair: RequestResponsePair = require(fileName)
        let request = mockRequest(pair.liveRequest)
        await coordinator.genStatefulResponse(
            request,
            response,
            genFakeResponses(),
            statefulProfile
        )
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())

        // read
        fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        pair = require(fileName)
        request = mockRequest(pair.liveRequest)
        await coordinator.genStatefulResponse(
            request,
            response,
            genFakeResponses(),
            statefulProfile
        )
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())

        // delete
        fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input_delete.json')
        pair = require(fileName)
        request = mockRequest(pair.liveRequest)
        await coordinator.genStatefulResponse(
            request,
            response,
            genFakeResponses(),
            statefulProfile
        )
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())
    })

    it('stateful with cascadeEnabled: create subresource should be failed if parent resource has not been created', async () => {
        const response = mockDefaultResponse()

        //create rg
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForCreateRG()),
            response,
            genFakeResponses(),
            statefulProfile
        )

        // create resource user without create it's parent resource service
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_create.json'
        )
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        expect(
            coordinator.genStatefulResponse(request, response, genFakeResponses(), statefulProfile)
        ).rejects.toThrow(NoParentResource)
    })

    it('stateful with cascadeEnabled: deleting a parent resource will also delete its child', async () => {
        const response = mockDefaultResponse()

        //create rg and service before test
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForCreateRG()),
            response,
            genFakeResponses(),
            statefulProfile
        )
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForCreateApiManagementService()),
            response,
            genFakeResponses(),
            statefulProfile
        )

        // create
        let fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input_create.json')
        let pair: RequestResponsePair = require(fileName)
        let request = mockRequest(pair.liveRequest)
        await coordinator.genStatefulResponse(
            request,
            response,
            genFakeResponses(),
            statefulProfile
        )
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())

        // delete parent resource
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForDeleteApiManagementService()),
            response,
            genFakeResponses(),
            statefulProfile
        )
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())

        // reading the child should be failed
        fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        pair = require(fileName)
        request = mockRequest(pair.liveRequest)
        expect(
            coordinator.genStatefulResponse(request, response, genFakeResponses(), statefulProfile)
        ).rejects.toThrow(ResourceNotFound)
    })
})

describe('genStatefulResponse() with cascadeEnabled==false', () => {
    const specRetriever = new SpecRetrieverFilesystem(config)
    const coordinator = new Coordinator(config, specRetriever, new ResponseGenerator())

    beforeAll(async () => {
        config.cascadeEnabled = false
        await coordinator.initialize()
    })

    beforeEach(async () => {
        coordinator.initiateResourcePool()
    })

    it('stateful with cascadeEnabled==false: create subresource can succeed even if parent resource has not been created', async () => {
        const response = mockDefaultResponse()

        //create rg
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForCreateRG()),
            response,
            genFakeResponses(),
            statefulProfile
        )

        // create resource user without create it's parent resource service
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_create.json'
        )
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        await coordinator.genStatefulResponse(
            request,
            response,
            genFakeResponses(),
            statefulProfile
        )
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())
    })

    it('stateful with cascadeEnabled==false: deleting a parent resource will not delete its child', async () => {
        const response = mockDefaultResponse()

        //create rg and service before test
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForCreateRG()),
            response,
            genFakeResponses(),
            statefulProfile
        )
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForCreateApiManagementService()),
            response,
            genFakeResponses(),
            statefulProfile
        )

        // create
        let fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input_create.json')
        let pair: RequestResponsePair = require(fileName)
        let request = mockRequest(pair.liveRequest)
        await coordinator.genStatefulResponse(
            request,
            response,
            genFakeResponses(),
            statefulProfile
        )
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())

        // delete parent resource
        await coordinator.genStatefulResponse(
            mockRequest(createLiveRequestForDeleteApiManagementService()),
            response,
            genFakeResponses(),
            statefulProfile
        )
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())

        // reading the child should be failed
        fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        pair = require(fileName)
        request = mockRequest(pair.liveRequest)
        await coordinator.genStatefulResponse(
            request,
            response,
            genFakeResponses(),
            statefulProfile
        )
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())
    })
})

describe('Example Validation', () => {
    const specRetriever = new SpecRetrieverFilesystem(config)
    const coordinator = new Coordinator(config, specRetriever, new ResponseGenerator())

    beforeAll(async () => {
        config.cascadeEnabled = false
        await coordinator.initialize()
    })

    beforeEach(async () => {
        coordinator.initiateResourcePool()
    })

    it('valid example request', async () => {
        const response = mockDefaultResponse()

        // create resource user without create it's parent resource service
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_create.json'
        )
        const pair: RequestResponsePair = require(fileName)
        if (!pair.liveRequest.headers) {
            pair.liveRequest.headers = {}
        }
        pair.liveRequest.headers['example-id'] = 'ApiManagementCreateUserBasic'
        const request = mockRequest(pair.liveRequest)
        await coordinator.generateResponse(request, response, statelessProfile)
        assert.strictEqual(response.statusCode, HttpStatusCode.OK.toString())
    })

    it('will fail if send wrong example-id', async () => {
        const response = mockDefaultResponse()

        // create resource user without create it's parent resource service
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_create.json'
        )
        const pair: RequestResponsePair = require(fileName)
        if (!pair.liveRequest.headers) {
            pair.liveRequest.headers = {}
        }
        pair.liveRequest.headers['example-id'] = 'Wrong-ApiManagementCreateUserBasic'
        const request = mockRequest(pair.liveRequest)
        await expect(
            coordinator.generateResponse(request, response, statelessProfile)
        ).rejects.toThrow(ExampleNotFound)
    })

    it("will fail if request body don't match example", async () => {
        const response = mockDefaultResponse()

        // create resource user without create it's parent resource service
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_create.json'
        )
        const pair: RequestResponsePair = require(fileName)
        if (!pair.liveRequest.headers) {
            pair.liveRequest.headers = {}
        }
        pair.liveRequest.headers['example-id'] = 'ApiManagementCreateUserBasic'
        if (!pair.liveRequest.body) {
            pair.liveRequest.body = {}
        }
        ;(pair.liveRequest.body as any)['properties']['firstName'] = 'fooName'
        const request = mockRequest(pair.liveRequest)
        await expect(
            coordinator.generateResponse(request, response, statelessProfile)
        ).rejects.toThrow(ExampleNotMatch)
    })

    it("will fail if request header don't match example", async () => {
        const response = mockDefaultResponse()

        // create resource user without create it's parent resource service
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_create.json'
        )
        const pair: RequestResponsePair = require(fileName)
        if (!pair.liveRequest.headers) {
            pair.liveRequest.headers = {}
        }
        pair.liveRequest.headers['example-id'] = 'ApiManagementCreateUserBasic'
        pair.liveRequest.headers['If-Match'] = 'fakedValue'
        const request = mockRequest(pair.liveRequest)
        await expect(
            coordinator.generateResponse(request, response, statelessProfile)
        ).rejects.toThrow(ExampleNotMatch)
    })
})
