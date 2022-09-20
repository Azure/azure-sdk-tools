import 'reflect-metadata' // Must be imported exactly once for Inversify

/* eslint-disable @typescript-eslint/no-var-requires */
import * as assert from 'assert'
import * as fs from 'fs'
import * as path from 'path'
import * as process from 'process'
import * as request from 'request'
import { HttpStatusCode } from '../../src/common/errors'
import { LiveValidator, RequestResponsePair } from 'oav/dist/lib/liveValidation/liveValidator'
import { config } from '../../src/common/index'
import {
    createLiveRequestForCreateApiManagementService,
    createLiveRequestForCreateRG
} from '../tools'
import { exec } from 'child_process'

const specDir = path.join(__dirname, '../../test/testData/swaggers')
const optionsForTest = {
    directory: specDir
}
const requestOptions: Record<string, any> = {
    json: true
}

async function startVirtualServer() {
    const cmd = `node dist/src/main.js`
    return await new Promise<void>((resolve) => {
        const serverProcesss = exec(cmd, {
            cwd: path.join(__dirname, '..', '..')
        })

        serverProcesss.stderr.on('data', (data) => {
            console.error(data)
        })

        serverProcesss.stdout.on('data', (data) => {
            if (Buffer.isBuffer(data)) data = data.toString()
            if (typeof data === 'string' && data.indexOf('validator initialized') >= 0) {
                return resolve()
            }
        })
    })
}

function createTestUrl(path: string, port = config.httpPortStateless): string {
    return `http://localhost:${port}${path}`
}

function createTestUrlWithHttps(path: string, port = config.httpsPortStateless): string {
    return `https://localhost:${port}${path}`
}

describe('Start virtual server and send requests', () => {
    const validator = new LiveValidator(optionsForTest)

    beforeAll(async () => {
        await startVirtualServer()
        requestOptions.ca = fs.readFileSync(
            path.join(__dirname, '..', '..', '.ssh', 'localhost-ca.crt')
        )
        await validator.initialize()
        process.env['NODE_TLS_REJECT_UNAUTHORIZED'] = '1'
    })

    afterAll(async () => {
        request.post(createTestUrl('/mock-service-host/shutdown'), requestOptions)
    })

    it('stateless request', (done) => {
        const fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        const pair: RequestResponsePair = require(fileName)
        request.get(
            createTestUrlWithHttps(pair.liveRequest.url),
            requestOptions,
            async (err, res, body) => {
                pair.liveResponse.body = body
                const response = validator.validateLiveResponse(pair.liveResponse, {
                    url: pair.liveRequest.url,
                    method: pair.liveRequest.method
                })
                assert.doesNotReject(response)
                const responseValidationResult = await response
                assert.strictEqual(res.statusCode, HttpStatusCode.OK)
                assert.strictEqual(responseValidationResult.isSuccessful, true)
                done()
            }
        )
    })

    it('stateless HTTP request', (done) => {
        const fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        const pair: RequestResponsePair = require(fileName)
        request.get(createTestUrl(pair.liveRequest.url), requestOptions, async (err, res, body) => {
            pair.liveResponse.body = body
            const response = validator.validateLiveResponse(pair.liveResponse, {
                url: pair.liveRequest.url,
                method: pair.liveRequest.method
            })
            assert.doesNotReject(response)
            const responseValidationResult = await response
            assert.strictEqual(res.statusCode, HttpStatusCode.OK)
            assert.strictEqual(responseValidationResult.isSuccessful, true)
            done()
        })
    })

    it('alwaysError request', (done) => {
        const fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        const pair: RequestResponsePair = require(fileName)
        request.get(
            createTestUrlWithHttps(pair.liveRequest.url, config.internalErrorPort),
            requestOptions,
            async (err, res, body) => {
                pair.liveResponse.body = body
                const responseValidationResult = await validator.validateLiveResponse(
                    pair.liveResponse,
                    {
                        url: pair.liveRequest.url,
                        method: pair.liveRequest.method
                    }
                )
                assert.strictEqual(res.statusCode, HttpStatusCode.INTERNAL_SERVER)
                assert.strictEqual(responseValidationResult.isSuccessful, false)
                done()
            }
        )
    })

    it('stateful get before put', (done) => {
        const fileName = path.join(__dirname, '..', 'testData', 'payloads', 'valid_input.json')
        const pair: RequestResponsePair = require(fileName)
        request.get(
            createTestUrlWithHttps(pair.liveRequest.url, config.httpsPortStateful),
            requestOptions,
            (err, res, body) => {
                assert.strictEqual(res.statusCode, HttpStatusCode.RESOURCE_NOT_FOUND)
                done()
            }
        )
    })

    it('stateful delete before put', (done) => {
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_delete.json'
        )
        const pair: RequestResponsePair = require(fileName)
        request.delete(
            createTestUrlWithHttps(pair.liveRequest.url, config.httpsPortStateful),
            {
                ...requestOptions,
                body: pair.liveRequest.body,
                headers: pair.liveRequest.headers
            },
            (err, res, body) => {
                assert.strictEqual(res.statusCode, HttpStatusCode.RESOURCE_NOT_FOUND)
                done()
            }
        )
    })

    it('stateful put->get->delete', (done) => {
        const createRGRequest = createLiveRequestForCreateRG()
        request.put(
            createTestUrlWithHttps(createRGRequest.url, config.httpsPortStateful),
            { ...requestOptions, json: createRGRequest.body },
            (err, res, body) => {
                const createServiceRequest = createLiveRequestForCreateApiManagementService()
                request.put(
                    createTestUrlWithHttps(createServiceRequest.url, config.httpsPortStateful),
                    {
                        ...requestOptions,
                        json: createServiceRequest.body,
                        qs: createServiceRequest.query
                    },
                    (err, res, body) => {
                        let fileName = path.join(
                            __dirname,
                            '..',
                            'testData',
                            'payloads',
                            'valid_input_create.json'
                        )
                        let pair: RequestResponsePair = require(fileName)
                        request.put(
                            createTestUrlWithHttps(pair.liveRequest.url, config.httpsPortStateful),
                            requestOptions,
                            (err, res, body) => {
                                assert.strictEqual(res.statusCode, HttpStatusCode.OK)

                                fileName = path.join(
                                    __dirname,
                                    '..',
                                    'testData',
                                    'payloads',
                                    'valid_input.json'
                                )
                                pair = require(fileName)
                                request.get(
                                    createTestUrlWithHttps(
                                        pair.liveRequest.url,
                                        config.httpsPortStateful
                                    ),
                                    requestOptions,
                                    (err, res, body) => {
                                        assert.strictEqual(res.statusCode, HttpStatusCode.OK)
                                        fileName = path.join(
                                            __dirname,
                                            '..',
                                            'testData',
                                            'payloads',
                                            'valid_input_delete.json'
                                        )
                                        pair = require(fileName)
                                        request.delete(
                                            createTestUrlWithHttps(
                                                pair.liveRequest.url,
                                                config.httpsPortStateful
                                            ),
                                            {
                                                ...requestOptions,
                                                body: pair.liveRequest.body,
                                                headers: pair.liveRequest.headers
                                            },
                                            (err, res, body) => {
                                                assert.strictEqual(
                                                    res.statusCode,
                                                    HttpStatusCode.OK
                                                )
                                                done()
                                            }
                                        )
                                    }
                                )
                            }
                        )
                    }
                )
            }
        )
    })

    it('special endpoints use json response', async () => {
        const endpoints = [
            '/common/.well-known/openid-configuration',
            '/metadata/endpoints',
            '/mock/oauth2/token',
            '/mock/oauth2/v2.0/token',
            '/mock/servicePrincipals',
            '/subscriptions',
            '/subscriptions/xxx',
            '/subscriptions/0000/locations',
            '/subscriptions/mock/providers',
            '/tenants'
        ]

        // on stateful endpoint
        for (const endpoint of endpoints) {
            request.get(
                createTestUrlWithHttps(endpoint, config.httpsPortStateful),
                requestOptions,
                (err, res, body) => {
                    assert.strictEqual(res.statusCode, HttpStatusCode.OK)
                    assert.strictEqual(typeof body, 'object')
                }
            )
        }

        // on stateless endpoint
        for (const endpoint of endpoints) {
            request.get(
                createTestUrlWithHttps(endpoint, config.httpsPortStateless),
                requestOptions,
                (err, res, body) => {
                    assert.strictEqual(res.statusCode, HttpStatusCode.OK)
                    assert.strictEqual(typeof body, 'object')
                }
            )
        }
    })
})
