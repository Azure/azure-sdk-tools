/* eslint-disable @typescript-eslint/no-var-requires */
import * as assert from 'assert'
import * as fs from 'fs'
import * as lodash from 'lodash'
import * as path from 'path'
import { LiveRequest } from 'oav/dist/lib/liveValidation/operationValidator'
import { RequestResponsePair } from 'oav/dist/lib/liveValidation/liveValidator'
import { VirtualServerRequest, VirtualServerResponse } from '../src/mid/models'
import { createErrorBody } from '../src/common/errors'

export function deleteFolderRecursive(directoryPath: string) {
    if (fs.existsSync(directoryPath)) {
        fs.readdirSync(directoryPath).forEach((file, index) => {
            const curPath = path.join(directoryPath, file)
            if (fs.lstatSync(curPath).isDirectory()) {
                // recurse
                deleteFolderRecursive(curPath)
            } else {
                // delete file
                fs.unlinkSync(curPath)
            }
        })
        fs.rmdirSync(directoryPath)
    }
}

export function mockRequest(req: LiveRequest, protocol = 'https'): VirtualServerRequest {
    return {
        query: req.query,
        url: req.url,
        protocol: protocol,
        method: req.method,
        headers: req.headers,
        body: req.body
    } as VirtualServerRequest
}

export function mockDefaultResponse(): VirtualServerResponse {
    return new VirtualServerResponse('500', createErrorBody(500, 'Default Response'))
}

export function storeAndCompare(
    pair: RequestResponsePair,
    response: VirtualServerResponse,
    path: string
) {
    const expected = lodash.cloneDeep(pair)
    pair.liveResponse.statusCode = response.statusCode
    pair.liveResponse.body = response.body
    pair.liveResponse.headers = response.headers

    const newFile = path + '.new'
    fs.writeFileSync(newFile, JSON.stringify(pair, null, 2)) // save new response for trouble shooting
    assert.deepStrictEqual(pair, expected)
    fs.unlinkSync(newFile) // remove the new file if pass the assert
}

export function createLiveRequestForCreateRG(sub = 'randomSub', rg = 'randomRG'): LiveRequest {
    return {
        url: `/subscriptions/${sub}/resourceGroups/${rg}`,
        method: 'PUT',
        body: {},
        query: {
            'api-version': '2018-01-01'
        }
    }
}

export function createLiveRequestForCreateApiManagementService(
    sub = 'randomSub',
    rg = 'randomRG',
    service = 'randomService'
): LiveRequest {
    return {
        url: `/subscriptions/${sub}/resourceGroups/${rg}/providers/Microsoft.ApiManagement/service/${service}`,
        method: 'PUT',
        body: {
            location: 'West US',
            sku: {
                name: 'Premium',
                capacity: 1
            },
            properties: {
                publisherEmail: 'admin@live.com',
                publisherName: 'contoso'
            }
        },
        query: {
            'api-version': '2018-01-01'
        }
    }
}

export function createLiveRequestForDeleteApiManagementService(
    sub = 'randomSub',
    rg = 'randomRG',
    service = 'randomService'
): LiveRequest {
    return {
        url: `/subscriptions/${sub}/resourceGroups/${rg}/providers/Microsoft.ApiManagement/service/${service}`,
        method: 'DELETE',
        body: {},
        query: {
            'api-version': '2018-01-01'
        }
    }
}

export function genFakeResponses() {
    return {
        200: 'faked 200',
        404: 'faked 204',
        500: 'faked 500'
    }
}
