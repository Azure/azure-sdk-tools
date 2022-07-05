import 'reflect-metadata' // Must be imported exactly once for Inversify

/* eslint-disable @typescript-eslint/no-var-requires */
import * as assert from 'assert'
import * as fs from 'fs'
import * as path from 'path'
import { Coordinator } from '../../src/mid/coordinator'
import { RequestResponsePair } from 'oav/dist/lib/liveValidation/liveValidator'
import { ResponseGenerator } from '../../src/mid/responser'
import { SpecRetrieverFilesystem } from '../../src/lib/specRetrieverFilesystem'
import { config } from '../../src/common/index'
import { deleteFolderRecursive, mockDefaultResponse, mockRequest } from '../tools'

const statelessProfile = {
    stateful: false
}

describe('example generation:', () => {
    const specRetriever = new SpecRetrieverFilesystem(config)
    const coordinator = new Coordinator(config, specRetriever, new ResponseGenerator(config))
    beforeAll(async () => {
        await coordinator.initialize()
    })
    it('generate example file if enableExampleGeneration is true:', async () => {
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_create.json'
        )
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        config.exampleGenerationFolder = 'tmp'
        config.enableExampleGeneration = true
        const exampleFolder = path.join(
            path.resolve(config.specRetrievalLocalRelativePath),
            'specification',
            'apimanagement',
            'resource-manager',
            'Microsoft.ApiManagement',
            'preview',
            '2018-01-01',
            config.exampleGenerationFolder
        )
        deleteFolderRecursive(exampleFolder)

        await coordinator.generateResponse(request, response, statelessProfile)
        assert.strictEqual(response.statusCode, pair.liveResponse.statusCode)
        pair.liveResponse.body = response.body
        pair.liveResponse.headers = response.headers
        pair.liveResponse.headers['Content-Type'] = 'application/json'
        let result = await coordinator.Validator.validateLiveRequestResponse(pair)
        assert.ok(result.requestValidationResult.isSuccessful)
        assert.ok(result.responseValidationResult.isSuccessful)

        // check example file
        let exampleFile = path.join(exampleFolder, 'User_CreateOrUpdate_1_gen.json')
        assert.ok(fs.existsSync(exampleFile), "example file isn't generated!")
        const exampleContent = require(exampleFile)
        pair.liveResponse.body = exampleContent.responses['200'].body
        result = await coordinator.Validator.validateLiveRequestResponse(pair)
        assert.ok(result.requestValidationResult.isSuccessful)
        assert.ok(result.responseValidationResult.isSuccessful)

        await coordinator.generateResponse(request, response, statelessProfile)
        exampleFile = path.join(exampleFolder, 'User_CreateOrUpdate_2_gen.json')
        assert.ok(fs.existsSync(exampleFile), "example file #2 isn't generated!")

        // remove generated example file
        deleteFolderRecursive(exampleFolder)
    })

    it('do NOT generate example file if enableExampleGeneration is false:', async () => {
        const fileName = path.join(
            __dirname,
            '..',
            'testData',
            'payloads',
            'valid_input_create.json'
        )
        const pair: RequestResponsePair = require(fileName)
        const request = mockRequest(pair.liveRequest)
        const response = mockDefaultResponse()
        config.exampleGenerationFolder = 'tmp'
        config.enableExampleGeneration = false
        await coordinator.generateResponse(request, response, statelessProfile)
        assert.strictEqual(response.statusCode, pair.liveResponse.statusCode)
        pair.liveResponse.body = response.body
        pair.liveResponse.headers = response.headers
        pair.liveResponse.headers['Content-Type'] = 'application/json'
        const result = await coordinator.Validator.validateLiveRequestResponse(pair)
        assert.ok(result.requestValidationResult.isSuccessful)
        assert.ok(result.responseValidationResult.isSuccessful)

        // check example file
        const exampleFolder = path.join(
            path.resolve(config.specRetrievalLocalRelativePath),
            'specification',
            'apimanagement',
            'resource-manager',
            'Microsoft.ApiManagement',
            'preview',
            '2018-01-01',
            config.exampleGenerationFolder
        )
        const exampleFile = path.join(exampleFolder, 'User_CreateOrUpdate_1_gen.json')
        assert.ok(!fs.existsSync(exampleFile), 'example file is generated!')
    })
})
