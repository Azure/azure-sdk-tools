/* eslint-disable @typescript-eslint/no-var-requires */
import * as fs from 'fs'
import * as path from 'path'
import { LiveRequest } from 'oav/dist/lib/liveValidation/operationValidator'
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
